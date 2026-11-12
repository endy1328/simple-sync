namespace SimpleSync;

/// <summary>Runs all supported directions through the same inventory/transfer/mirror engine.</summary>
public sealed class SyncService
{
    public Task<SyncResult> SyncAsync(IEnumerable<SyncPair> pairs, CancellationToken cancellationToken)
        => SyncAsync(pairs, null, cancellationToken);

    public Task<SyncResult> SyncAsync(IEnumerable<SyncPair> pairs, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var snapshot = pairs.Where(pair => pair.Enabled).ToList();
        return Task.Run(async () =>
        {
            var combined = new SyncResult();
            foreach (var pair in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SyncResult result;
                try
                {
                    if (string.IsNullOrWhiteSpace(pair.Source) || string.IsNullOrWhiteSpace(pair.Target))
                        throw new IOException("Choose both folders. / 소스와 타겟 폴더를 선택하세요.");
                    if (pair.SourceType == StorageTypes.Local && pair.TargetType == StorageTypes.Local)
                    {
                        var source = new LocalStorageEndpoint(pair.Source);
                        var target = new LocalStorageEndpoint(pair.Target);
                        if (source.Root.StartsWith(target.Root, StringComparison.OrdinalIgnoreCase) ||
                            target.Root.StartsWith(source.Root, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Source and target overlap. / 소스와 타겟이 서로 포함하는 경로입니다.");
                        if (!Directory.Exists(source.Root)) throw new DirectoryNotFoundException("Source folder not found. / 소스 폴더 없음");
                        target.CreateTargetRoot();
                        result = await new EndpointSyncService().SyncAsync(pair, source, target, progress, cancellationToken);
                    }
                    else
                    {
                        if (!((pair.SourceType == StorageTypes.Local && pair.TargetType == StorageTypes.GoogleDrive) ||
                              (pair.SourceType == StorageTypes.GoogleDrive && pair.TargetType == StorageTypes.Local)))
                            throw new IOException("Unsupported direction. / 지원하지 않는 동기화 방향입니다.");
                        if (string.IsNullOrWhiteSpace(pair.DriveAccountId))
                            throw new IOException("Choose a Google account. / Google 계정을 선택하세요.");
                        using var accounts = new GoogleAccountService();
                        using var client = await accounts.CreateAuthenticatedClientAsync(pair.DriveAccountId, cancellationToken);
                        var remoteSource = pair.SourceType == StorageTypes.GoogleDrive;
                        var hashCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleSync", "LocalHashes");
                        IStorageEndpoint source = remoteSource
                            ? new GoogleDriveEndpoint(client, pair.Source, accountId: pair.DriveAccountId)
                            : new LocalStorageEndpoint(pair.Source, hashCache);
                        IStorageEndpoint target = remoteSource
                            ? new LocalStorageEndpoint(pair.Target, hashCache)
                            : new GoogleDriveEndpoint(client, pair.Target, exportGoogleDocuments: false, accountId: pair.DriveAccountId);
                        if (target is LocalStorageEndpoint local) local.CreateTargetRoot();
                        result = await new EndpointSyncService().SyncAsync(pair, source, target, progress, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    result = new SyncResult { FailedFiles = 1 };
                    result.Messages.Add(ex.Message);
                    progress?.Report(new SyncProgress { Pair = pair, Phase = SyncPhase.Failed, FailedFiles = 1, Message = ex.Message });
                }
                combined.CopiedFiles += result.CopiedFiles;
                combined.SkippedFiles += result.SkippedFiles;
                combined.DeletedFiles += result.DeletedFiles;
                combined.ExcludedFiles += result.ExcludedFiles;
                combined.FailedFiles += result.FailedFiles;
                combined.Messages.AddRange(result.Messages);
            }
            return combined;
        }, cancellationToken);
    }
}
