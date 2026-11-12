using System.Security.Cryptography;
using System.Text;
using SimpleSync;

public static class ConcurrentSyncTests
{
    public static async Task RunAsync()
    {
        await ExistingFoldersAreNotPrepared();
        await BoundedDirectoryPreparation(8, 4);
        await BoundedDirectoryPreparation(2, 2);
        await BoundedDirectoryPreparation(1, 1);
        await BoundedDirectoryPreparation(4, 1, capable: false);
        await DirectoryFailureAndCancellationJoinWorkers(cancel: false);
        await DirectoryFailureAndCancellationJoinWorkers(cancel: true);
        await BoundedTransfers(4, 4);
        await BoundedTransfers(8, 4);
        await BoundedTransfers(4, 2, sourceLimit: 2);
        await BoundedTransfers(4, 4, sourceOnly: true);
        await DirectoriesArePreparedBeforeTransfers();
        await BoundedTransfers(2, 2);
        await FailureWaitsForWorkers();
        await CancellationWaitsForWorkers();
        await SerialTransfers(native: true, capable: true);
        await SerialTransfers(native: false, capable: false);
        await SerialTransfers(native: false, capable: true, large: true);
        await MixedTransfersRespectBarriers();
    }

    private static async Task ExistingFoldersAreNotPrepared()
    {
        var source = new Endpoint([new("folder", "folder", true)]);
        var target = new Endpoint([new("folder", "folder", true)]);
        var result = await Run(source, target);
        Check(result.FailedFiles == 0 && target.Ensures == 0, "existing folders must not call EnsureDirectory");
    }

    private static async Task BoundedDirectoryPreparation(int limit, int expected, bool capable = true)
    {
        var full = Gate(); var release = Gate();
        var completed = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        var active = 0; var maximum = 0; var started = 0; var writes = 0;
        Endpoint target = capable ? new DirectoryEndpoint([], limit) : new Endpoint([]);
        target.Prepare = async (path, token) =>
        {
            var current = Interlocked.Increment(ref active);
            lock (target) maximum = Math.Max(maximum, current);
            if (path.Contains('/')) Check(completed.Count >= 8, "all parent folders must finish before the child level");
            if (Interlocked.Increment(ref started) == expected) full.TrySetResult();
            await release.Task.WaitAsync(token);
            completed[path] = true;
            Interlocked.Decrement(ref active);
        };
        target.Write = (_, _) =>
        {
            Check(completed.Count == 16 && active == 0, "all folders must finish before any upload");
            Interlocked.Increment(ref writes);
            return Task.CompletedTask;
        };
        var folders = Enumerable.Range(0, 8).SelectMany(i => new[] {
            new StorageEntry("p" + i, "folder" + i, true), new StorageEntry("c" + i, "folder" + i + "/child", true) });
        var run = Run(new Endpoint(folders.Concat(Files(1)).ToArray()), target);
        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(active == expected && maximum == expected && writes == 0, "folder workers obey the target limit and cap four");
        }
        finally { release.TrySetResult(); }
        var result = await run;
        Check(result.FailedFiles == 0 && result.CopiedFiles == 1 && maximum <= expected, "bounded folder preparation finishes safely");
    }

    private static async Task DirectoryFailureAndCancellationJoinWorkers(bool cancel)
    {
        var secondEntered = Gate(); var held = Gate(); var failed = Gate();
        using var cancellation = new CancellationTokenSource();
        var target = new DirectoryEndpoint([new("extra", "extra.txt", false)], 2);
        var writes = 0;
        target.Prepare = async (path, _) =>
        {
            if (path == "a")
            {
                await secondEntered.Task;
                if (!cancel) { failed.TrySetResult(); throw new IOException("folder preparation failed"); }
                await held.Task;
            }
            else { secondEntered.TrySetResult(); await held.Task; }
        };
        target.Write = (_, _) => { Interlocked.Increment(ref writes); return Task.CompletedTask; };
        var source = new Endpoint(new[] { new StorageEntry("a", "a", true), new StorageEntry("b", "b", true) }.Concat(Files(1)).ToArray());
        var run = Run(source, target, cancellation.Token);
        try
        {
            await (cancel ? secondEntered.Task : failed.Task).WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) cancellation.Cancel();
            Check(!run.IsCompleted && writes == 0 && target.Deletes == 0, "failed or cancelled folder preparation must join active workers before returning");
        }
        finally { held.TrySetResult(); }
        if (cancel)
        {
            try { await run; throw new Exception("folder cancellation must propagate"); }
            catch (OperationCanceledException) { }
        }
        else Check((await run).FailedFiles > 0, "folder preparation failure must be reported");
        Check(writes == 0 && target.Deletes == 0 && source.Lists == 1, "folder failure or cancellation must prevent uploads and mirror deletion");
    }

    private static async Task MixedTransfersRespectBarriers()
    {
        var beforeRelease = Gate(); var beforeFull = Gate();
        var nativeRelease = Gate(); var nativeEntered = Gate();
        var afterRelease = Gate(); var afterFull = Gate();
        var beforeActive = 0; var beforeStarted = 0; var afterStarted = 0; var nativeActive = 0;
        var target = new ConcurrentEndpoint([]);
        target.Write = async (path, _) =>
        {
            if (path is "0.txt" or "1.txt")
            {
                Interlocked.Increment(ref beforeActive);
                if (Interlocked.Increment(ref beforeStarted) == 2) beforeFull.TrySetResult();
                await beforeRelease.Task;
                Interlocked.Decrement(ref beforeActive);
            }
            else if (path == "2.txt")
            {
                Interlocked.Increment(ref nativeActive);
                nativeEntered.TrySetResult();
                await nativeRelease.Task;
                Interlocked.Decrement(ref nativeActive);
            }
            else
            {
                if (Interlocked.Increment(ref afterStarted) == 2) afterFull.TrySetResult();
                await afterRelease.Task;
            }
        };
        var entries = Files(5);
        entries[2] = entries[2] with { Md5 = null, MimeType = "application/vnd.google-apps.document" };
        var run = Run(new Endpoint(entries), target);
        try
        {
            await beforeFull.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(!nativeEntered.Task.IsCompleted && afterStarted == 0, "native export waits for preceding small-file workers");
            beforeRelease.TrySetResult();
            await nativeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(beforeActive == 0 && afterStarted == 0, "native export never overlaps either small-file batch");
            nativeRelease.TrySetResult();
            await afterFull.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(nativeActive == 0, "following small-file workers wait for native export");
        }
        finally { beforeRelease.TrySetResult(); nativeRelease.TrySetResult(); afterRelease.TrySetResult(); }
        var result = await run;
        Check(result.CopiedFiles == 5 && result.FailedFiles == 0, "mixed transfer batches complete accurately");
    }

    private static async Task DirectoriesArePreparedBeforeTransfers()
    {
        var prepared = new List<string>();
        var target = new ConcurrentEndpoint([]);
        target.Ensure = path =>
        {
            Check(prepared.Count == 0 || prepared[^1] == "parent", "missing folders prepare in parent order");
            prepared.Add(path);
        };
        target.Write = (_, _) =>
        {
            Check(prepared.SequenceEqual(new[] { "parent", "parent/child" }), "all missing folders precede transfers");
            return Task.CompletedTask;
        };
        var files = Files(2).Select(e => e with { RelativePath = "parent/child/" + e.RelativePath });
        var source = new Endpoint(new[] { new StorageEntry("child", "parent/child", true), new StorageEntry("parent", "parent", true) }.Concat(files).ToArray());
        Check((await Run(source, target)).CopiedFiles == 2, "prepared folder transfers succeed");
    }

    private static async Task BoundedTransfers(int capability, int expected, int? sourceLimit = null, bool sourceOnly = false)
    {
        var release = Gate();
        var full = Gate();
        var active = 0;
        var maximum = 0;
        var started = 0;
        Endpoint target = sourceOnly ? new Endpoint([]) : new ConcurrentEndpoint([], capability);
        target.Write = async (_, _) =>
        {
            var current = Interlocked.Increment(ref active);
            lock (target) maximum = Math.Max(maximum, current);
            if (Interlocked.Increment(ref started) == expected) full.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref active);
        };
        var reports = new List<SyncProgress>();
        var callbacks = 0;
        var callbackOverlap = 0;
        Endpoint source = sourceOnly || sourceLimit is not null ? new ConcurrentEndpoint(Files(12), sourceLimit ?? capability) : new Endpoint(Files(12));
        var run = Run(source, target, progress: new InlineProgress(p =>
        {
            if (Interlocked.Increment(ref callbacks) != 1) Interlocked.Exchange(ref callbackOverlap, 1);
            try { reports.Add(p); }
            finally { Interlocked.Decrement(ref callbacks); }
        }));
        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(active == expected && maximum <= expected, "bounded transfer concurrency follows capability and cap four");
        }
        finally { release.TrySetResult(); }
        var result = await run;
        Check(result.CopiedFiles == 12 && result.FailedFiles == 0 && maximum == expected, "all bounded transfers finish");
        Check(reports.Zip(reports.Skip(1)).All(p => p.First.ProcessedFiles <= p.Second.ProcessedFiles), "progress counts remain monotonic");
        Check(reports[^1].ProcessedFiles == 12, "terminal progress includes all files");
        Check(callbackOverlap == 0, "progress callbacks are serialized");
    }

    private static async Task FailureWaitsForWorkers()
    {
        var held = Gate(); var both = Gate(); var failed = Gate();
        var target = new ConcurrentEndpoint([new("extra", "extra.txt", false)]);
        target.Write = async (path, _) =>
        {
            if (path == "0.txt") { await both.Task; failed.TrySetResult(); throw new IOException("planned failure"); }
            both.TrySetResult(); await held.Task;
        };
        var reports = new List<SyncProgress>();
        var source = new Endpoint(Files(2));
        var run = Run(source, target, progress: new InlineProgress(p => reports.Add(p)));
        try
        {
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(!run.IsCompleted && target.Deletes == 0 && source.Lists == 1, "failure waits for active upload before completion or mirror rescan");
        }
        finally { both.TrySetResult(); held.TrySetResult(); }
        var result = await run;
        Check(result.FailedFiles == 1 && result.CopiedFiles == 1 && target.Deletes == 0, "failed transfer suppresses mirror deletion");
        Check(reports[^1].ProcessedFiles == 2, "failed files count as processed");
    }

    private static async Task CancellationWaitsForWorkers()
    {
        var entered = Gate(); var held = Gate();
        using var cancellation = new CancellationTokenSource();
        var target = new ConcurrentEndpoint([new("extra", "extra.txt", false)]);
        target.Write = async (_, _) => { entered.TrySetResult(); await held.Task; };
        SyncPhase? terminal = null;
        var run = Run(new Endpoint(Files(8)), target, cancellation.Token, new InlineProgress(p => terminal = p.Phase));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Check(!run.IsCompleted && terminal != SyncPhase.Cancelled && target.Deletes == 0, "cancellation waits before terminal progress");
        held.TrySetResult();
        try { await run; throw new Exception("expected cancellation"); }
        catch (OperationCanceledException) { }
        Check(terminal == SyncPhase.Cancelled && target.Deletes == 0, "cancelled workers suppress deletion");
    }

    private static async Task SerialTransfers(bool native, bool capable, bool large = false)
    {
        var release = Gate(); var entered = Gate(); var writes = 0;
        Endpoint target = capable ? new ConcurrentEndpoint([]) : new Endpoint([]);
        target.Write = async (_, _) => { Interlocked.Increment(ref writes); entered.TrySetResult(); await release.Task; };
        var files = Files(3).Select(e => e with { Md5 = native ? null : e.Md5, Size = large ? 1024 * 1024 + 1 : e.Size,
            MimeType = native ? "application/vnd.google-apps.document" : null }).ToArray();
        var run = Run(new Endpoint(files), target);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(writes == 1, "native exports, large files and endpoints without capability remain serial");
        release.TrySetResult();
        Check((await run).CopiedFiles == 3, "serial transfers finish");
    }

    private static async Task<SyncResult> Run(IStorageEndpoint source, IStorageEndpoint target,
        CancellationToken token = default, IProgress<SyncProgress>? progress = null)
    {
        var state = Path.Combine(Path.GetTempPath(), "concurrent-sync-" + Guid.NewGuid().ToString("N"));
        try { return await new EndpointSyncService(state).SyncAsync(new SyncPair { Source = "source", Target = "target", Mode = SyncModes.Mirror }, source, target, progress, token); }
        finally { if (Directory.Exists(state)) Directory.Delete(state, true); }
    }

    private static StorageEntry[] Files(int count) => Enumerable.Range(0, count).Select(i =>
        new StorageEntry(i.ToString(), i + ".txt", false, 7, Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("payload"))), "v1")).ToArray();
    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class InlineProgress(Action<SyncProgress> report) : IProgress<SyncProgress> { public void Report(SyncProgress value) => report(value); }

    private class Endpoint(IReadOnlyList<StorageEntry> entries) : IStorageEndpoint
    {
        public int Ensures, Deletes, Lists;
        public Func<string, CancellationToken, Task>? Write;
        public Action<string>? Ensure;
        public Func<string, CancellationToken, Task>? Prepare;
        public Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken token) { Interlocked.Increment(ref Lists); return Task.FromResult(entries); }
        public async Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken token) => await destination.WriteAsync(Encoding.UTF8.GetBytes("payload"), token);
        public Task WriteAsync(string path, StorageEntry? existing, Stream source, CancellationToken token) => Write?.Invoke(path, token) ?? Task.CompletedTask;
        public Task EnsureDirectoryAsync(string path, CancellationToken token) { Interlocked.Increment(ref Ensures); Ensure?.Invoke(path); return Prepare?.Invoke(path, token) ?? Task.CompletedTask; }
        public Task DeleteAsync(StorageEntry entry, CancellationToken token) { Interlocked.Increment(ref Deletes); return Task.CompletedTask; }
    }
    private sealed class ConcurrentEndpoint(IReadOnlyList<StorageEntry> entries, int limit = 4) : Endpoint(entries), IConcurrentTransferEndpoint
    { public int MaxConcurrentTransfers => limit; }
    private sealed class DirectoryEndpoint(IReadOnlyList<StorageEntry> entries, int limit) : Endpoint(entries), IConcurrentDirectoryEndpoint
    { public int MaxConcurrentDirectories => limit; }
}
