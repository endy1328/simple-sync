using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SimpleSync;

public static class StoragePath
{
    public static string Validate(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || Path.IsPathRooted(path))
            throw new IOException($"Invalid relative path: {relativePath}");
        foreach (var part in path.Split('/'))
        {
            var stem = part.Split('.')[0];
            if (part is "" or "." or ".." || part.EndsWith(' ') || part.EndsWith('.') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3])))
                throw new IOException($"Unsupported Windows path: {relativePath}");
        }
        return path;
    }
}

public sealed class LocalStorageEndpoint(string root) : IStorageEndpoint
{
    public string Root { get; } = NormalizeRoot(root);

    private static string NormalizeRoot(string value)
    {
        var input = value.Replace('/', '\\');
        if (input.StartsWith("\\\\?\\", StringComparison.Ordinal) || input.StartsWith("\\\\.\\", StringComparison.Ordinal) || input.StartsWith("\\??\\", StringComparison.Ordinal))
            throw new IOException("Windows device/extended namespace paths are not supported.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        ValidateAncestors(full);
        var ancestor = Path.TrimEndingDirectorySeparator(full);
        while (!Directory.Exists(ancestor))
            ancestor = Path.GetDirectoryName(ancestor) ?? throw new IOException("No accessible parent directory.");
        using var handle = CreateFile(ancestor, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException("Cannot resolve folder identity.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        var buffer = new StringBuilder(32768);
        var count = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (count == 0 || count >= buffer.Capacity) throw new IOException("Cannot resolve canonical folder path.");
        var canonical = buffer.ToString();
        if (canonical.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) canonical = "\\\\" + canonical[8..];
        else if (canonical.StartsWith("\\\\?\\", StringComparison.Ordinal)) canonical = canonical[4..];
        var suffix = Path.GetRelativePath(ancestor, full);
        if (suffix != ".") canonical = Path.Combine(canonical, StoragePath.Validate(suffix));
        return canonical.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);

    public void CreateTargetRoot()
    {
        ValidateAncestors(Root.TrimEnd(Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Root);
    }

    public void PreserveModifiedTime(string relativePath, string sourceVersion)
    {
        if (long.TryParse(sourceVersion, out var ticks))
            File.SetLastWriteTimeUtc(Resolve(relativePath), new DateTime(ticks, DateTimeKind.Utc));
    }

    private string Resolve(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(Root, StoragePath.Validate(relativePath)));
        if (!path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escapes root.");
        ValidateAncestors(path);
        return path;
    }

    private static void ValidateAncestors(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic links and junctions are not supported: {current}");
            // Include ancestors above the root too: a selected root must not traverse a junction.
        }
    }

    public async Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken cancellationToken)
    {
        ValidateAncestors(Root.TrimEnd(Path.DirectorySeparatorChar));
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);
        var entries = new List<StorageEntry>();
        async Task Walk(string directory)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
                var attributes = File.GetAttributes(path);
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                if (isDirectory && !filter.ShouldTraverseDirectory(relative)) continue;
                if (!isDirectory && !filter.ShouldCopyFile(relative))
                {
                    entries.Add(new(path, relative, false));
                    continue;
                }
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Unsupported link: {relative}");
                StoragePath.Validate(relative);
                if (isDirectory)
                {
                    entries.Add(new(path, relative, true));
                    await Walk(path);
                }
                else
                {
                    var info = new FileInfo(path);
                    var version = info.LastWriteTimeUtc.Ticks.ToString();
                    var length = info.Length;
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
                    var md5 = Convert.ToHexString(await MD5.HashDataAsync(stream, cancellationToken));
                    info.Refresh();
                    if (info.Length != length || info.LastWriteTimeUtc.Ticks.ToString() != version) throw new IOException($"File changed during scan: {relative}");
                    entries.Add(new(path, relative, false, length, md5, version));
                }
            }
        }
        await Walk(Root);
        return entries;
    }

    public async Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(Resolve(entry.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task WriteAsync(string relativePath, StorageEntry? existing, Stream source, CancellationToken cancellationToken)
    {
        var path = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".simple-sync.tmp";
        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                await source.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Resolve(relativePath);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public Task EnsureDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Resolve(relativePath));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(StorageEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(entry.RelativePath);
        if (entry.IsDirectory)
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path, recursive: false);
        }
        else File.Delete(path);
        return Task.CompletedTask;
    }
}
