using SimpleSync;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

public static class DriveInventoryCacheTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "drive-inventory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var server = new DriveEndpointTests.FakeDrive();
            for (var i = 0; i < 20; i++)
            {
                server.Add("d" + i, "folder" + i, GoogleDriveEndpoint.FolderMimeType);
                server.Add("f" + i, "note.md", content: "initial", parent: "d" + i);
            }
            GoogleDriveEndpoint Endpoint(string account = "account") => new(server.Client, "root", false, account, root);
            var filter = SyncFilter.FromPair(new SyncPair());
            var first = await Endpoint().ListAsync(filter, default);
            Check(first.Count == 40 && server.ChildRequests == 21, "initial inventory must scan every folder");
            server.ResetCounts();
            var warm = await Endpoint().ListAsync(filter, default);
            Check(warm.Count == 40 && server.ChildRequests == 0 && server.ChangeRequests == 1,
                "restarting an unchanged endpoint must consume the change feed without listing folders");
            server.Files["f2"].Content = Encoding.UTF8.GetBytes("changed");
            server.Changed("f2");
            server.ResetCounts();
            var changed = await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 1 && changed.Single(e => e.Id == "f2").Size == 7,
                "file changes must refresh only their affected parent");
            server.Files.Remove("f3"); server.Changed("f3", removed: true);
            Check(!(await Endpoint().ListAsync(filter, default)).Any(e => e.Id == "f3"), "remote removals must invalidate cached files");
            server.Files["d4"].Name = "renamed"; server.Changed("d4");
            server.ResetCounts();
            Check((await Endpoint().ListAsync(filter, default)).Any(e => e.RelativePath == "renamed/note.md") && server.ChildRequests == 21,
                "folder rename must rebuild paths from a full inventory");
            server.InvalidChangeToken = true; server.ResetCounts();
            Check((await Endpoint().ListAsync(filter, default)).Count == 39 && server.ChildRequests == 21,
                "invalid cursors must fall back to a full scan");
            server.BrokenChangePage = true; server.ResetCounts();
            await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 21, "incomplete change feed must not validate stale inventory");
            server.BrokenChangePage = false;
            server.ResetCounts();
            await Endpoint("different-account").ListAsync(filter, default);
            Check(server.ChildRequests == 21, "accounts must never share inventory snapshots");
            server.ResetCounts();
            await Endpoint().ListAsync(SyncFilter.FromPair(new SyncPair { ExcludePatterns = ["folder1/**"] }), default);
            Check(server.ChildRequests > 1, "filter changes must rebuild the scoped snapshot");
            foreach (var file in Directory.GetFiles(root, "*.json")) File.WriteAllText(file, "corrupt");
            server.ResetCounts();
            await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 21, "corrupt snapshots must recover with a full scan");
        }
        finally { Directory.Delete(root, true); }
        await SourcePriorityRepairsRemoteChanges();
        await PaginationFailureAndAgeRecovery();
        await ChangesDuringFullScanAreReplayed();
        await MirrorUsesFreshSourceInventory();
        await MirrorWithoutDeletesSkipsSecondScan();
    }

    private static async Task SourcePriorityRepairsRemoteChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "incremental-priority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var local = Path.Combine(root, "local"); Directory.CreateDirectory(local);
            File.WriteAllText(Path.Combine(local, "note.md"), "source wins");
            using var server = new DriveEndpointTests.FakeDrive();
            server.Add("original", "note.md", content: "source wins");
            var pair = new SyncPair { Source = local, Target = "root", SourceType = StorageTypes.Local,
                TargetType = StorageTypes.GoogleDrive, DriveAccountId = "account", Mode = SyncModes.Copy };
            var engine = new EndpointSyncService(Path.Combine(root, "state"));
            LocalStorageEndpoint Source() => new(local, Path.Combine(root, "local-cache"));
            GoogleDriveEndpoint Target() => new(server.Client, "root", false, "account", Path.Combine(root, "drive-cache"));
            Check((await engine.SyncAsync(pair, Source(), Target(), null, default)).SkippedFiles == 1, "initial equal source must be skipped");
            server.Files["original"].Content = "remote edit"u8.ToArray(); server.Changed("original");
            var source = Source();
            var repaired = await engine.SyncAsync(pair, source, Target(), null, default);
            Check(repaired.CopiedFiles == 1 && repaired.FailedFiles == 0 && Encoding.UTF8.GetString(server.Files["original"].Content) == "source wins",
                "unchanged local source must overwrite remote-only edits");
            Check(source.ReusedHashes == 1, "remote-only changes must not require rehashing unchanged local files");
            server.Files.Remove("original"); server.Changed("original", removed: true);
            var restored = await engine.SyncAsync(pair, Source(), Target(), null, default);
            Check(restored.CopiedFiles == 1 && restored.FailedFiles == 0 && server.Files.Values.Single().Name == "note.md",
                "unchanged local source must recreate a remotely deleted target");
            server.Add("extra", "extra.md"); server.Changed("extra");
            pair.Mode = SyncModes.Mirror;
            var mirrored = await engine.SyncAsync(pair, Source(), Target(), null, default);
            Check(mirrored.DeletedFiles == 1 && mirrored.FailedFiles == 0 && !server.Files.ContainsKey("extra"),
                "incremental target inventory must retain source-first mirror deletion");
            server.ResetCounts();
            Check((await engine.SyncAsync(pair, Source(), Target(), null, default)).SkippedFiles == 1, "warm mirror must skip unchanged file");
            server.ResetCounts();
            var steady = await engine.SyncAsync(pair, Source(), Target(), null, default);
            Check(steady.FailedFiles == 0 && steady.CopiedFiles == 0 && server.ChildRequests == 0,
                "unchanged target after own writes must settle to change-feed-only inventory");
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task PaginationFailureAndAgeRecovery()
    {
        var root = Path.Combine(Path.GetTempPath(), "incremental-feed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var server = new DriveEndpointTests.FakeDrive();
            server.Add("folder", "folder", GoogleDriveEndpoint.FolderMimeType);
            server.Add("a", "a.md"); server.Add("b", "b.md");
            GoogleDriveEndpoint Endpoint(HttpClient? http = null) => new(http ?? server.Client, "root", false, "account", root);
            var filter = SyncFilter.FromPair(new SyncPair());
            await Endpoint().ListAsync(filter, default);
            var cache = Directory.GetFiles(root, "*.json").Single();
            var initial = File.ReadAllText(cache);
            var firstPayload = JsonNode.Parse(JsonNode.Parse(initial)!["Payload"]!.GetValue<string>())!;
            server.Files["a"].Content = "new-a"u8.ToArray(); server.Changed("a");
            server.Files["b"].Parent = "folder"; server.Changed("b");
            server.Add("new", "new.md", parent: "folder"); server.Changed("new");
            server.ChangePageSize = 1; server.ResetCounts();
            var entries = await Endpoint().ListAsync(filter, default);
            Check(server.ChangeRequests == 3 && server.ChildRequests == 2 && entries.Any(e => e.RelativePath == "folder/b.md") && entries.Any(e => e.Id == "new"),
                "all feed pages must be processed before refreshing both old/new parents");
            var payload = JsonNode.Parse(JsonNode.Parse(File.ReadAllText(cache))!["Payload"]!.GetValue<string>())!;
            Check(payload["FullScannedAt"]!.ToJsonString() == firstPayload["FullScannedAt"]!.ToJsonString(), "warm updates cannot postpone full-scan expiry");
            server.RepeatChangePage = true; server.ResetCounts();
            await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 2, "repeated feed cursor must cause full recovery");
            server.RepeatChangePage = false;
            server.MalformedChangeJson = true; server.ResetCounts();
            await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 2, "malformed change JSON must cause full recovery");
            server.MalformedChangeJson = false;
            foreach (var malformed in new[] { "[]", "{\"changes\":[null],\"newStartPageToken\":\"3\"}",
                "{\"changes\":[{\"fileId\":\"a\",\"file\":{}}],\"newStartPageToken\":\"3\"}" })
            {
                server.ChangeBodyOverride = malformed; server.ResetCounts();
                await Endpoint().ListAsync(filter, default);
                Check(server.ChildRequests == 2, "malformed change structures and metadata must trigger full recovery");
            }
            server.ChangeBodyOverride = null;
            var before = File.ReadAllText(cache);
            server.ForbiddenReason = "insufficientPermissions";
            try { await Endpoint().ListAsync(filter, default); throw new Exception("expected permission failure"); }
            catch (IOException) { }
            Check(File.ReadAllText(cache) == before, "permission failure must preserve the last complete snapshot");
            server.ForbiddenReason = null;
            using var blocker = new ChangeBlocker(server);
            using var http = new HttpClient(blocker, disposeHandler: false);
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = Endpoint(http).ListAsync(filter, cancellation.Token);
                await blocker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                try { await pending; throw new Exception("expected cancellation"); }
                catch (OperationCanceledException) { }
                Check(File.ReadAllText(cache) == before, "in-flight feed cancellation must not advance the saved cursor");
            }
            var envelope = JsonNode.Parse(File.ReadAllText(cache))!;
            payload = JsonNode.Parse(envelope["Payload"]!.GetValue<string>())!;
            payload["FullScannedAt"] = DateTimeOffset.UtcNow.AddDays(-8);
            envelope["Payload"] = payload.ToJsonString();
            envelope["Sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString())));
            File.WriteAllText(cache, envelope.ToJsonString());
            server.ResetCounts();
            await Endpoint().ListAsync(filter, default);
            Check(server.ChildRequests == 2, "valid but expired snapshots must receive a full verification");
            server.ResetCounts();
            await Endpoint().ListFreshAsync(filter, default);
            Check(server.ChildRequests == 2, "fresh source verification must bypass valid snapshots");
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task MirrorUsesFreshSourceInventory()
    {
        using var server = new DriveEndpointTests.FakeDrive();
        server.Add("extra", "extra.md");
        var source = new FreshAwareSource();
        var result = await new EndpointSyncService().SyncAsync(new SyncPair { Source = "source", Target = "root", Mode = SyncModes.Mirror },
            source, new GoogleDriveEndpoint(server.Client, "root"), null, default);
        Check(result.FailedFiles > 0 && source.FreshReads == 1 && server.Deleted.Count == 0,
            "mirror must use a fresh source inventory and suppress deletion after source changes");
    }

    private static async Task MirrorWithoutDeletesSkipsSecondScan()
    {
        using var server = new DriveEndpointTests.FakeDrive();
        server.Add("same", "note.md");
        var source = new FreshAwareSource();
        var result = await new EndpointSyncService().SyncAsync(new SyncPair { Source = "source", Target = "root", Mode = SyncModes.Mirror },
            source, new GoogleDriveEndpoint(server.Client, "root"), null, default);
        Check(result.FailedFiles == 0 && source.FreshReads == 0 && result.SkippedFiles == 1,
            "mirror without deletion candidates must not perform a redundant full source rescan");
    }

    private static async Task ChangesDuringFullScanAreReplayed()
    {
        var root = Path.Combine(Path.GetTempPath(), "incremental-baseline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var server = new DriveEndpointTests.FakeDrive();
            server.Add("d0", "first", GoogleDriveEndpoint.FolderMimeType);
            server.Add("d1", "second", GoogleDriveEndpoint.FolderMimeType);
            using var mutation = new ScanMutation(server);
            using var http = new HttpClient(mutation, disposeHandler: false);
            var filter = SyncFilter.FromPair(new SyncPair());
            var initial = await new GoogleDriveEndpoint(http, "root", false, "account", root).ListAsync(filter, default);
            Check(!initial.Any(e => e.Id == "late"), "test mutation occurs after its parent folder was scanned");
            var replayed = await new GoogleDriveEndpoint(http, "root", false, "account", root).ListAsync(filter, default);
            Check(replayed.Any(e => e.RelativePath == "first/late.md"), "cursor captured before scanning must replay mutations during a full baseline");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class ScanMutation(DriveEndpointTests.FakeDrive server) : DelegatingHandler(server)
    {
        private bool _mutated;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (!_mutated && Uri.UnescapeDataString(request.RequestUri!.Query).Contains("'d1' in parents", StringComparison.Ordinal))
            {
                _mutated = true;
                server.Add("late", "late.md", parent: "d0"); server.Changed("late");
            }
            return base.SendAsync(request, token);
        }
    }

    private sealed class FreshAwareSource : IStorageEndpoint, IFreshInventoryEndpoint
    {
        public int FreshReads;
        public Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<StorageEntry>>([new("planned", "note.md", false, 0, Convert.ToHexString(MD5.HashData([])))]);
        public Task<IReadOnlyList<StorageEntry>> ListFreshAsync(SyncFilter filter, CancellationToken token)
        { FreshReads++; return Task.FromResult<IReadOnlyList<StorageEntry>>([]); }
        public Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken token) => Task.CompletedTask;
        public Task WriteAsync(string path, StorageEntry? existing, Stream source, CancellationToken token) => throw new NotSupportedException();
        public Task EnsureDirectoryAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(StorageEntry entry, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class ChangeBlocker(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/changes"))
            { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            return await base.SendAsync(request, token);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
