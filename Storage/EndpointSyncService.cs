using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleSync;

/// <summary>Plans complete endpoint inventories before changing either destination.</summary>
public sealed class EndpointSyncService(string? stateDirectory = null)
{
    public sealed record ExportState(string SourceId, string Version, string TargetMd5);
    private readonly string _stateDirectory = stateDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleSync", "SyncState");

    public async Task<SyncResult> SyncAsync(SyncPair pair, IStorageEndpoint source, IStorageEndpoint target,
        IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var result = new SyncResult();
        var processed = 0;
        var total = 0;
        var progressLock = new object();
        void Report(SyncPhase phase, string? path = null, long bytes = 0, long? length = null)
        {
            lock (progressLock)
                progress?.Report(new SyncProgress { Pair = pair, Phase = phase, CurrentPath = path,
                ProcessedFiles = processed, TotalFiles = total, CopiedFiles = result.CopiedFiles,
                SkippedFiles = result.SkippedFiles, DeletedFiles = result.DeletedFiles,
                ExcludedFiles = result.ExcludedFiles, FailedFiles = result.FailedFiles,
                CurrentFileBytes = bytes, CurrentFileTotalBytes = length });
        }
        try
        {
            Report(SyncPhase.Preparing);
            if (source is LocalStorageEndpoint localSource && target is LocalStorageEndpoint localTarget &&
                (localSource.Root.StartsWith(localTarget.Root, StringComparison.OrdinalIgnoreCase) ||
                 localTarget.Root.StartsWith(localSource.Root, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Source and target overlap.");
            var filter = SyncFilter.FromPair(pair);
            var sourceEntries = await source.ListAsync(filter, cancellationToken);
            var targetEntries = await target.ListAsync(filter, cancellationToken);
            var sources = Inventory(sourceEntries);
            var targets = Inventory(targetEntries);
            foreach (var entry in sources.Values)
            {
                if (targets.TryGetValue(entry.RelativePath, out var existing) && entry.IsDirectory != existing.IsDirectory)
                    throw new IOException($"File/folder type conflict: {entry.RelativePath}");
            }
            var sourceFiles = sources.Values.Where(entry => !entry.IsDirectory && filter.ShouldCopyFile(entry.RelativePath)).ToList();
            total = sourceFiles.Count;
            result.ExcludedFiles = sources.Values.Count(entry => !entry.IsDirectory) - total;
            var stateKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
                pair.SourceType, pair.Source, pair.TargetType, pair.Target, pair.DriveAccountId))));
            var statePath = Path.Combine(_stateDirectory, stateKey + ".json");
            var state = LoadState(statePath);
            var nextState = new Dictionary<string, ExportState>(StringComparer.OrdinalIgnoreCase);

            foreach (var directory in sources.Values.Where(entry => entry.IsDirectory).OrderBy(entry => entry.RelativePath.Count(c => c == '/')))
                if (!targets.ContainsKey(directory.RelativePath))
                {
                    Report(SyncPhase.Preparing, directory.RelativePath);
                    await target.EnsureDirectoryAsync(directory.RelativePath, cancellationToken);
                }

            async Task TransferAsync(StorageEntry entry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                targets.TryGetValue(entry.RelativePath, out var existing);
                var equal = entry.Md5 is not null && existing?.Md5 is not null && entry.Md5.Equals(existing.Md5, StringComparison.OrdinalIgnoreCase);
                if (entry.Md5 is null && existing?.Md5 is not null && state.TryGetValue(entry.RelativePath, out var saved) &&
                    saved.SourceId == entry.Id && saved.Version == entry.Version && !string.IsNullOrEmpty(entry.Version) &&
                    saved.TargetMd5.Equals(existing.Md5, StringComparison.OrdinalIgnoreCase))
                {
                    equal = true;
                    lock (progressLock) nextState[entry.RelativePath] = saved;
                }
                if (equal)
                {
                    lock (progressLock)
                    {
                        result.SkippedFiles++;
                        processed++;
                        Report(SyncPhase.Copying, entry.RelativePath);
                    }
                    return;
                }
                try
                {
                    Report(SyncPhase.Copying, entry.RelativePath, length: entry.Size);
                    var spool = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".simple-sync.tmp");
                    await using var buffer = new FileStream(spool, FileMode.CreateNew, FileAccess.ReadWrite,
                        FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                    await source.ReadAsync(entry, buffer, cancellationToken);
                    buffer.Position = 0;
                    var hash = Convert.ToHexString(await MD5.HashDataAsync(buffer, cancellationToken));
                    if (entry.Md5 is not null && !hash.Equals(entry.Md5, StringComparison.OrdinalIgnoreCase))
                        throw new IOException($"Source changed during transfer: {entry.RelativePath}");
                    buffer.Position = 0;
                    await target.WriteAsync(entry.RelativePath, existing, buffer, cancellationToken);
                    if (source is LocalStorageEndpoint && target is LocalStorageEndpoint localTargetEndpoint)
                        localTargetEndpoint.PreserveModifiedTime(entry.RelativePath, entry.Version);
                    lock (progressLock)
                    {
                        if (entry.Md5 is null) nextState[entry.RelativePath] = new(entry.Id, entry.Version, hash);
                        result.CopiedFiles++;
                        processed++;
                        Report(SyncPhase.Copying, entry.RelativePath, buffer.Length, buffer.Length);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lock (progressLock)
                    {
                        result.FailedFiles++;
                        processed++;
                        result.Messages.Add($"{entry.RelativePath}: {ex.Message}");
                        Report(SyncPhase.Copying, entry.RelativePath);
                    }
                }
            }

            var limits = new[] { source, target }.OfType<IConcurrentTransferEndpoint>()
                .Select(endpoint => Math.Clamp(endpoint.MaxConcurrentTransfers, 1, 4)).ToArray();
            var concurrency = limits.Length == 0 ? 1 : limits.Min();
            bool CanTransferConcurrently(StorageEntry entry) => concurrency > 1 && entry.Md5 is not null &&
                entry.Size is >= 0 and <= 1024 * 1024 &&
                !(entry.MimeType?.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) ?? false);

            // Parallel.ForEachAsync owns a fixed worker set and joins every worker even on cancellation.
            // Serial entries form barriers so native exports and large files never overlap a transfer.
            for (var index = 0; index < sourceFiles.Count;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CanTransferConcurrently(sourceFiles[index]))
                {
                    await TransferAsync(sourceFiles[index++]);
                    continue;
                }
                var start = index;
                while (index < sourceFiles.Count && CanTransferConcurrently(sourceFiles[index])) index++;
                await Parallel.ForEachAsync(sourceFiles.GetRange(start, index - start),
                    new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = cancellationToken },
                    async (entry, _) => await TransferAsync(entry));
            }

            if (result.FailedFiles == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sourceFiles.Any(entry => entry.Md5 is null)) SaveState(statePath, nextState);
                if (SyncModes.Normalize(pair.Mode) == SyncModes.Mirror)
                {
                    // A valid initial snapshot is insufficient if the source changes while transferring.
                    var latestSources = Inventory(await source.ListAsync(filter, cancellationToken));
                    if (sources.Count != latestSources.Count || sources.Any(item =>
                        !latestSources.TryGetValue(item.Key, out var current) ||
                        current.Id != item.Value.Id || current.IsDirectory != item.Value.IsDirectory ||
                        current.Version != item.Value.Version || !string.Equals(current.Md5, item.Value.Md5, StringComparison.OrdinalIgnoreCase)))
                        throw new IOException("Source changed during synchronization; mirror deletion skipped. / 소스 변경으로 미러링 삭제 생략");
                    foreach (var entry in targets.Values.Where(entry => !entry.IsDirectory && !sources.ContainsKey(entry.RelativePath) && filter.ShouldDeleteTargetFile(entry.RelativePath)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Report(SyncPhase.Deleting, entry.RelativePath);
                        await target.DeleteAsync(entry, cancellationToken);
                        result.DeletedFiles++;
                    }
                    foreach (var entry in targets.Values.Where(entry => entry.IsDirectory && !sources.ContainsKey(entry.RelativePath) && filter.ShouldDeleteTargetDirectory(entry.RelativePath))
                        .OrderByDescending(entry => entry.RelativePath.Count(c => c == '/')))
                        await target.DeleteAsync(entry, cancellationToken);
                }
            }
            else result.Messages.Add("Mirror deletion skipped because transfer failed. / 전송 실패로 미러링 삭제 생략");
        }
        catch (OperationCanceledException) { Report(SyncPhase.Cancelled); throw; }
        catch (Exception ex) { result.FailedFiles++; result.Messages.Add(ex.Message); }
        Report(result.FailedFiles == 0 ? SyncPhase.Completed : SyncPhase.Failed);
        return result;
    }

    private static Dictionary<string, StorageEntry> Inventory(IReadOnlyList<StorageEntry> entries)
    {
        var result = new Dictionary<string, StorageEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var path = StoragePath.Validate(entry.RelativePath);
            if (!result.TryAdd(path, entry with { RelativePath = path })) throw new IOException($"Ambiguous file name: {path}");
        }
        foreach (var path in result.Keys)
        {
            var parent = path;
            while (parent.Contains('/'))
            {
                parent = parent[..parent.LastIndexOf('/')];
                if (result.TryGetValue(parent, out var entry) && !entry.IsDirectory) throw new IOException($"File blocks parent path: {parent}");
            }
        }
        return result;
    }

    private static Dictionary<string, ExportState> LoadState(string path)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, ExportState>>(File.ReadAllText(path)) ?? []; }
        catch (Exception ex) when (ex is IOException or JsonException) { return []; }
    }

    private void SaveState(string path, Dictionary<string, ExportState> state)
    {
        Directory.CreateDirectory(_stateDirectory);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(state)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
