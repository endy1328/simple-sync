namespace SimpleSync;

public static class StorageTypes
{
    public const string Local = "local";
    public const string GoogleDrive = "google_drive";
}

public sealed record StorageEntry(string Id, string RelativePath, bool IsDirectory,
    long Size = 0, string? Md5 = null, string Version = "", string? MimeType = null);

/// <summary>An endpoint is bound to one validated root. Paths use forward slashes.</summary>
public interface IStorageEndpoint
{
    Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken cancellationToken);
    Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken cancellationToken);
    Task WriteAsync(string relativePath, StorageEntry? existing, Stream source, CancellationToken cancellationToken);
    Task EnsureDirectoryAsync(string relativePath, CancellationToken cancellationToken);
    Task DeleteAsync(StorageEntry entry, CancellationToken cancellationToken);
}

/// <summary>Opt-in for endpoints whose transfer operations support concurrent callers after inventory/preparation.</summary>
public interface IConcurrentTransferEndpoint
{
    int MaxConcurrentTransfers { get; }
}

/// <summary>Opt-in for concurrent directory preparation; shared parent paths must be deduplicated.</summary>
public interface IConcurrentDirectoryEndpoint
{
    int MaxConcurrentDirectories { get; }
}

/// <summary>A fresh inventory bypasses persistent caches for destructive-operation revalidation.</summary>
public interface IFreshInventoryEndpoint
{
    Task<IReadOnlyList<StorageEntry>> ListFreshAsync(SyncFilter filter, CancellationToken cancellationToken);
}
