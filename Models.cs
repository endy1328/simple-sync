namespace SimpleSync;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 10;
    public string Skin { get; set; } = "syncback_blue";
    public List<SyncPair> Pairs { get; set; } = [];
}

public sealed class SyncPair
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = SyncModes.Copy;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
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
    public int FailedFiles { get; set; }
    public List<string> Messages { get; } = [];
}
