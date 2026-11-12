using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SimpleSync;

public static class LocalHashCacheTests
{
    public static async Task RunAsync()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "simple-sync-hashes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var root = Path.Combine(sandbox, "files");
            var cache = Path.Combine(sandbox, "cache");
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "one.txt");
            await File.WriteAllTextAsync(file, "aaaa");
            var filter = SyncFilter.FromPair(new SyncPair());
            var constructor = typeof(LocalStorageEndpoint).GetConstructor([typeof(string), typeof(string)]);
            Check(constructor is not null, "local endpoint supports an opt-in persistent hash cache");
            LocalStorageEndpoint Endpoint() => (LocalStorageEndpoint)constructor!.Invoke([root, cache]);
            int Count(LocalStorageEndpoint endpoint, string name) => (int)typeof(LocalStorageEndpoint).GetProperty(name)!.GetValue(endpoint)!;
            var endpoint = Endpoint();
            await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "HashedFiles") == 1, "first inventory hashes files");
            endpoint = Endpoint();
            await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "ReusedHashes") == 1, "new endpoint reuses persistent hashes on NTFS");
            var timestamp = File.GetLastWriteTimeUtc(file);
            await File.WriteAllTextAsync(file, "bbbb");
            File.SetLastWriteTimeUtc(file, timestamp);
            var changed = await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "HashedFiles") == 1 && changed.Single().Md5 == Convert.ToHexString(MD5.HashData("bbbb"u8)), "same size and restored mtime cannot hide content changes");
            File.Move(file, Path.Combine(root, "renamed.txt"));
            await File.WriteAllTextAsync(Path.Combine(root, "new.txt"), "new");
            var renamed = await endpoint.ListAsync(filter, default);
            Check(renamed.Count == 2 && renamed.All(entry => entry.RelativePath != "one.txt"), "rename and create inventory is current");
            File.Delete(Path.Combine(root, "new.txt"));
            Check((await endpoint.ListAsync(filter, default)).Count == 1, "deleted files leave the inventory");
            var cacheFile = Directory.GetFiles(cache, "*.json").Single();
            await File.WriteAllTextAsync(cacheFile, "corrupt");
            await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "HashedFiles") == 1, "corrupt cache forces hashing");
            var fresh = typeof(LocalStorageEndpoint).GetMethod("ListFreshAsync")!;
            await (Task<IReadOnlyList<StorageEntry>>)fresh.Invoke(endpoint, [filter, CancellationToken.None])!;
            Check(Count(endpoint, "HashedFiles") == 1 && Count(endpoint, "ReusedHashes") == 0, "fresh override hashes all files");
            var anchor = JsonNode.Parse(await File.ReadAllTextAsync(cacheFile))!["FullVerificationUtc"]!.ToJsonString();
            await endpoint.ListAsync(filter, default);
            Check(JsonNode.Parse(await File.ReadAllTextAsync(cacheFile))!["FullVerificationUtc"]!.ToJsonString() == anchor,
                "incremental scans do not postpone full verification");
            var document = JsonNode.Parse(await File.ReadAllTextAsync(cacheFile))!;
            document["FullVerificationUtc"] = DateTimeOffset.UtcNow.AddDays(-8);
            var unsigned = document.DeepClone();
            unsigned.AsObject().Remove("Integrity");
            document["Integrity"] = Convert.ToHexString(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(unsigned)));
            await File.WriteAllTextAsync(cacheFile, document.ToJsonString());
            await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "HashedFiles") == 1, "verification older than seven days forces full hash");
            document = JsonNode.Parse(await File.ReadAllTextAsync(cacheFile))!;
            document["Files"]!.AsObject().First().Value!["Md5"] = new string('0', 32);
            await File.WriteAllTextAsync(cacheFile, document.ToJsonString());
            var recovered = await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "HashedFiles") == 1 && recovered.Single().Md5 != new string('0', 32),
                "valid JSON with a corrupted cached hash must not authorize reuse");
            var before = await File.ReadAllTextAsync(cacheFile);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try { await endpoint.ListAsync(filter, cancellation.Token); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { }
            Check(await File.ReadAllTextAsync(cacheFile) == before, "cancelled inventory preserves complete cache");
            await using (var locked = new FileStream(Path.Combine(root, "renamed.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { await endpoint.ListAsync(filter, default); throw new Exception("Expected locked file failure"); }
                catch (IOException) { }
            }
            Check(await File.ReadAllTextAsync(cacheFile) == before, "failed inventory preserves complete cache");
            var uncached = new LocalStorageEndpoint(root);
            await uncached.ListAsync(filter, default);
            await uncached.ListAsync(filter, default);
            Check(Count(uncached, "HashedFiles") == 1 && Count(uncached, "ReusedHashes") == 0, "default endpoints always hash");
            var filtered = SyncFilter.FromPair(new SyncPair { Extensions = [".bin"] });
            Check((await endpoint.ListAsync(filtered, default)).Single().Md5 is null, "filtered files retain excluded inventory entries");
            await endpoint.ListAsync(filter, default);
            Check(Count(endpoint, "ReusedHashes") == 1, "separate filter inventories preserve matching hashes");
        }
        finally { Directory.Delete(sandbox, true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
