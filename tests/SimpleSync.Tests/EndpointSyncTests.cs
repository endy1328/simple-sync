using System.Security.Cryptography;
using System.Text;
using SimpleSync;

public static class EndpointSyncTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "endpoint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "source");
            var targetPath = Path.Combine(root, "target");
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(targetPath);
            var source = new LocalStorageEndpoint(sourcePath);
            var target = new LocalStorageEndpoint(targetPath);
            var pair = new SyncPair { Source = sourcePath, Target = targetPath, Mode = SyncModes.Mirror };
            var engine = new EndpointSyncService(Path.Combine(root, "state"));
            var trailingTarget = new LocalStorageEndpoint(Path.Combine(root, "new-target") + Path.DirectorySeparatorChar);
            trailingTarget.CreateTargetRoot();
            Check(Directory.Exists(Path.Combine(root, "new-target")) && trailingTarget.Root.EndsWith(Path.DirectorySeparatorChar), "new target accepts trailing directory separator");
            File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "source");
            File.WriteAllText(Path.Combine(targetPath, "file.txt"), "newer-target");
            File.SetLastWriteTimeUtc(Path.Combine(targetPath, "file.txt"), DateTime.UtcNow.AddDays(1));
            File.WriteAllText(Path.Combine(targetPath, "extra.txt"), "remove");
            File.WriteAllText(Path.Combine(targetPath, "keep.bin"), "keep");
            pair.Extensions = [".txt"];
            var result = await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(result.FailedFiles == 0 && result.CopiedFiles == 1 && result.DeletedFiles == 1, "mirror counts");
            Check(File.ReadAllText(Path.Combine(targetPath, "file.txt")) == "source", "source beats newer target");
            Check(File.GetLastWriteTimeUtc(Path.Combine(sourcePath, "file.txt")) == File.GetLastWriteTimeUtc(Path.Combine(targetPath, "file.txt")), "local timestamps preserved");
            Check(File.Exists(Path.Combine(targetPath, "keep.bin")), "excluded file preserved");
            result = await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(result.SkippedFiles == 1 && result.CopiedFiles == 0, "identical content skipped");

            Directory.CreateDirectory(Path.Combine(targetPath, "extra"));
            File.WriteAllText(Path.Combine(targetPath, "extra", "excluded.bin"), "keep");
            result = await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(result.FailedFiles == 0 && File.Exists(Path.Combine(targetPath, "extra", "excluded.bin")), "excluded descendants preserve target-only folder without failure");
            File.WriteAllText(Path.Combine(sourcePath, "cancel.txt"), "new");
            File.WriteAllText(Path.Combine(targetPath, "cancel-extra.txt"), "keep");
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelled = false;
                SyncPhase? terminalPhase = null;
                try
                {
                    await engine.SyncAsync(pair, source, target, new TestProgress(p =>
                    { terminalPhase = p.Phase; if (p.CopiedFiles > 0) cancellation.Cancel(); }), cancellation.Token);
                }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && File.Exists(Path.Combine(targetPath, "cancel-extra.txt")), "cancellation after transfer prevents deletion");
                Check(terminalPhase == SyncPhase.Cancelled, "cancelled execution reports terminal progress");
            }
            File.Delete(Path.Combine(sourcePath, "cancel.txt"));

            File.WriteAllText(Path.Combine(sourcePath, "trigger.txt"), "trigger");
            File.WriteAllText(Path.Combine(targetPath, "late.txt"), "preserve");
            var changedSource = false;
            result = await engine.SyncAsync(pair, source, target, new TestProgress(p =>
            {
                if (!changedSource && p.CopiedFiles > 0)
                {
                    changedSource = true;
                    File.WriteAllText(Path.Combine(sourcePath, "late.txt"), "new source");
                }
            }), CancellationToken.None);
            Check(result.FailedFiles > 0 && File.ReadAllText(Path.Combine(targetPath, "late.txt")) == "preserve", "source changes during transfer suppress mirror deletion");
            File.Delete(Path.Combine(sourcePath, "late.txt"));
            File.Delete(Path.Combine(sourcePath, "trigger.txt"));

            Directory.CreateDirectory(Path.Combine(targetPath, "file-block"));
            File.WriteAllText(Path.Combine(sourcePath, "file-block"), "conflict");
            result = await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(result.FailedFiles > 0 && File.Exists(Path.Combine(targetPath, "cancel-extra.txt")), "type conflicts detected before mutation");
            File.Delete(Path.Combine(sourcePath, "file-block"));

            File.WriteAllText(Path.Combine(targetPath, "extra.txt"), "preserve on error");
            result = await engine.SyncAsync(pair, new FaultEndpoint(), target, null, CancellationToken.None);
            Check(result.FailedFiles > 0 && File.Exists(Path.Combine(targetPath, "extra.txt")), "incomplete listing prevents deletion");
            result = await engine.SyncAsync(pair, new FaultEndpoint(failRead: true), target, null, CancellationToken.None);
            Check(result.FailedFiles > 0 && File.Exists(Path.Combine(targetPath, "extra.txt")), "failed transfer prevents deletion");

            result = await engine.SyncAsync(pair, new FaultEndpoint(duplicate: true), target, null, CancellationToken.None);
            Check(result.FailedFiles > 0 && File.Exists(Path.Combine(targetPath, "extra.txt")), "ambiguous source prevents mutation");

            var exports = new ExportEndpoint();
            pair.Extensions = [];
            pair.SourceType = StorageTypes.GoogleDrive;
            pair.Source = "remote-folder";
            pair.DriveAccountId = "account";
            result = await engine.SyncAsync(pair, exports, target, null, CancellationToken.None);
            Check(result.FailedFiles == 0 && File.Exists(Path.Combine(targetPath, "report.docx")), "native export written");
            await engine.SyncAsync(pair, exports, target, null, CancellationToken.None);
            Check(exports.ReadCount == 1, "unchanged native export uses recorded target hash");
            File.WriteAllText(Path.Combine(targetPath, "report.docx"), "edited-target");
            await engine.SyncAsync(pair, exports, target, null, CancellationToken.None);
            Check(exports.ReadCount == 2 && File.ReadAllText(Path.Combine(targetPath, "report.docx")) == "office", "target edits restored from source");

            pair.SourceType = StorageTypes.Local; pair.Source = sourcePath; pair.Mode = SyncModes.Copy;
            await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(File.Exists(Path.Combine(targetPath, "report.docx")), "copy preserves target-only files");
            pair.Mode = SyncModes.Mirror;
            File.Delete(Path.Combine(sourcePath, "file.txt"));
            result = await engine.SyncAsync(pair, source, target, null, CancellationToken.None);
            Check(result.FailedFiles == 0 && !File.Exists(Path.Combine(targetPath, "file.txt")), "valid empty source mirrors deletions");

            File.WriteAllText(Path.Combine(sourcePath, "local.txt"), "keep");
            result = await engine.SyncAsync(pair, source, new LocalStorageEndpoint(sourcePath), null, CancellationToken.None);
            Check(result.FailedFiles > 0 && File.Exists(Path.Combine(sourcePath, "local.txt")), "overlapping roots rejected");
            var extendedResult = await new SyncService().SyncAsync([new SyncPair { Source = sourcePath,
                Target = "\\\\?\\" + root, Mode = SyncModes.Mirror }], CancellationToken.None);
            Check(extendedResult.FailedFiles > 0 && File.Exists(Path.Combine(sourcePath, "local.txt")), "extended namespace cannot bypass source overlap protection");
            var unsafePath = false;
            try { await target.EnsureDirectoryAsync("../escape", CancellationToken.None); }
            catch (IOException) { unsafePath = true; }
            Check(unsafePath, "path traversal rejected");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private sealed class TestProgress(Action<SyncProgress> action) : IProgress<SyncProgress>
    { public void Report(SyncProgress value) => action(value); }

    private sealed class FaultEndpoint(bool failRead = false, bool duplicate = false) : IStorageEndpoint
    {
        public Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken token)
        {
            if (!failRead && !duplicate) throw new IOException("page two failed");
            IReadOnlyList<StorageEntry> entries = duplicate
                ? [new("1", "a.txt", false), new("2", "A.txt", false)]
                : [new("1", "file.txt", false, 1)];
            return Task.FromResult(entries);
        }
        public Task ReadAsync(StorageEntry entry, Stream target, CancellationToken token) => throw new IOException("transfer interrupted");
        public Task WriteAsync(string path, StorageEntry? entry, Stream source, CancellationToken token) => throw new NotSupportedException();
        public Task EnsureDirectoryAsync(string path, CancellationToken token) => Task.CompletedTask;
        public Task DeleteAsync(StorageEntry entry, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class ExportEndpoint : IStorageEndpoint
    {
        public int ReadCount;
        public Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken token)
            => Task.FromResult<IReadOnlyList<StorageEntry>>([new("doc-id", "report.docx", false, Version: "v1", MimeType: "application/vnd.google-apps.document")]);
        public async Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken token)
        { ReadCount++; await destination.WriteAsync(Encoding.UTF8.GetBytes("office"), token); }
        public Task WriteAsync(string path, StorageEntry? existing, Stream source, CancellationToken token) => throw new NotSupportedException();
        public Task EnsureDirectoryAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(StorageEntry entry, CancellationToken token) => throw new NotSupportedException();
    }
}
