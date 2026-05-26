namespace SimpleSync;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 10;
    public string Skin { get; set; } = "syncback_blue";
    public int WindowWidth { get; set; } = 1720;
    public int WindowHeight { get; set; } = 1120;
    public List<SyncPair> Pairs { get; set; } = [];
}

public sealed class SyncPair
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = SyncModes.Copy;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public List<string> IncludePatterns { get; set; } = [];
    public List<string> ExcludePatterns { get; set; } = [];
    public List<string> Extensions { get; set; } = [];
    public List<string> Files { get; set; } = [];
    public bool IncludeSubdirectories { get; set; } = true;
}

public static class SyncModes
{
    public const string Copy = "copy";
    public const string Mirror = "mirror";

    public static string Normalize(string? mode)
    {
        return string.Equals(mode, Mirror, StringComparison.OrdinalIgnoreCase) ? Mirror : Copy;
    }
}

public sealed class SyncResult
{
    public int CopiedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int DeletedFiles { get; set; }
    public int ExcludedFiles { get; set; }
    public int FailedFiles { get; set; }
    public List<string> Messages { get; } = [];
}

public enum SyncPhase
{
    Pending,
    Preparing,
    Copying,
    Deleting,
    Completed,
    Failed
}

public sealed class SyncProgress
{
    public SyncPair Pair { get; init; } = new();
    public SyncPhase Phase { get; init; } = SyncPhase.Pending;
    public int ProcessedFiles { get; init; }
    public int? TotalFiles { get; init; }
    public string? CurrentPath { get; init; }
    public long CurrentFileBytes { get; init; }
    public long? CurrentFileTotalBytes { get; init; }
    public int CopiedFiles { get; init; }
    public int SkippedFiles { get; init; }
    public int DeletedFiles { get; init; }
    public int ExcludedFiles { get; init; }
    public int FailedFiles { get; init; }
    public string? Message { get; init; }
}
