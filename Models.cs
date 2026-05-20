namespace SimpleSync;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 10;
    public string Skin { get; set; } = "syncback_blue";
    public List<SyncPair> Pairs { get; set; } = [];
}

public sealed class SyncPair
{
    public bool Enabled { get; set; } = true;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public sealed class SyncResult
{
    public int CopiedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int FailedFiles { get; set; }
    public List<string> Messages { get; } = [];
}
