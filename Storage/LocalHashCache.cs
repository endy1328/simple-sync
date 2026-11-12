using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace SimpleSync;

/// <summary>Disposable inventories are hints only; NTFS handle metadata authorizes each reuse.</summary>
internal sealed class LocalHashCache
{
    internal sealed record Fingerprint(ulong Volume, string FileId, long ChangeTime, long WriteTime, long Length);
    internal sealed record CachedFile(Fingerprint Identity, string Md5);
    internal sealed class Inventory
    {
        public int Schema { get; set; } = 1;
        public string Root { get; set; } = "";
        public DateTimeOffset FullVerificationUtc { get; set; }
        public string Integrity { get; set; } = "";
        public Dictionary<string, CachedFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly string _root;
    private readonly string _path;
    public Inventory Previous { get; }
    public Inventory Next { get; }
    public bool ReuseAllowed { get; }

    public LocalHashCache(string root, string directory, string filter, bool fresh)
    {
        _root = root;
        _path = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant() + "\n" + filter))) + ".json");
        Previous = Load();
        var now = DateTimeOffset.UtcNow;
        ReuseAllowed = !fresh && Previous.FullVerificationUtc <= now && now - Previous.FullVerificationUtc < TimeSpan.FromDays(7);
        Next = new() { Root = root, FullVerificationUtc = ReuseAllowed ? Previous.FullVerificationUtc : now };
    }

    private Inventory Load()
    {
        try
        {
            var result = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(_path));
            if (result is { Schema: 1, Files: not null } && string.Equals(result.Root, _root, StringComparison.OrdinalIgnoreCase) &&
                result.Integrity == IntegrityOf(result))
            {
                result.Files = new(result.Files, StringComparer.OrdinalIgnoreCase);
                return result;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        return new();
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Next.Integrity = IntegrityOf(Next);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, true))
            {
                await JsonSerializer.SerializeAsync(stream, Next, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, _path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Caching never makes an otherwise valid scan fail. */ }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static bool IsSupportedRoot(string root)
    {
        if (!OperatingSystem.IsWindows() || root.StartsWith("\\\\", StringComparison.Ordinal)) return false;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            return drive.DriveType is DriveType.Fixed or DriveType.Removable && drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    private static string IntegrityOf(Inventory inventory) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { inventory.Schema, inventory.Root, inventory.FullVerificationUtc, inventory.Files })));

    public static Fingerprint? ReadIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(handle, 0, out BasicInfo basic, (uint)Marshal.SizeOf<BasicInfo>()) ||
            !GetFileId(handle, 18, out IdInfo id, (uint)Marshal.SizeOf<IdInfo>()) ||
            !GetStandardInfo(handle, 1, out StandardInfo standard, (uint)Marshal.SizeOf<StandardInfo>()) ||
            basic.ChangeTime <= 0 || basic.LastWriteTime <= 0 || standard.Directory || standard.DeletePending ||
            (id.Low == 0 && id.High == 0)) return null;
        return new(id.Volume, $"{id.High:X16}{id.Low:X16}", basic.ChangeTime, basic.LastWriteTime, standard.EndOfFile);
    }

    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo { public long CreationTime, LastAccessTime, LastWriteTime, ChangeTime; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct IdInfo { public ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct StandardInfo
    {
        public long AllocationSize, EndOfFile;
        public uint NumberOfLinks;
        [MarshalAs(UnmanagedType.U1)] public bool DeletePending;
        [MarshalAs(UnmanagedType.U1)] public bool Directory;
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int type, out BasicInfo info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileId(SafeFileHandle handle, int type, out IdInfo info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetStandardInfo(SafeFileHandle handle, int type, out StandardInfo info, uint size);
}
