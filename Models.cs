namespace SimpleSync;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 10;
    public string Skin { get; set; } = "syncback_blue";
    public string Language { get; set; } = LocalizationService.DefaultLanguage;
    public int WindowWidth { get; set; } = 1720;
    public int WindowHeight { get; set; } = 1120;
    public List<FilterPreset> FilterPresets { get; set; } = FilterPreset.CreateDefaults();
    public List<SyncPair> Pairs { get; set; } = [];
}

public sealed class FilterPreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> Extensions { get; set; } = [];
    public List<string> Files { get; set; } = [];
    public List<string> IncludePatterns { get; set; } = [];
    public List<string> ExcludePatterns { get; set; } = [];

    public string Tooltip
    {
        get
        {
            var parts = new List<string>();
            AddPart(parts, "Extensions", Extensions);
            AddPart(parts, "Files", Files);
            AddPart(parts, "Include", IncludePatterns);
            AddPart(parts, "Exclude", ExcludePatterns);
            return parts.Count == 0 ? "No values configured." : string.Join(Environment.NewLine, parts);
        }
    }

    public static List<FilterPreset> CreateDefaults()
    {
        return
        [
            new() { Name = "Documents", Extensions = [".docx", ".xlsx", ".pdf", ".txt", ".md"] },
            new() { Name = "Images", Extensions = [".png", ".jpg", ".svg"] },
            new() { Name = "Code", Extensions = [".cs", ".js", ".json", ".xml", ".md"] },
            new() { Name = "Archives", Extensions = [".zip", ".7z", ".tar"] },
            new() { Name = "Exclude temp/build", ExcludePatterns = ["*.tmp", ".git/**", "bin/**", "obj/**", "node_modules/**"] }
        ];
    }

    public static List<FilterPreset> CreateDefaults(LocalizationService localization)
    {
        return CreateDefaults().Select(localization.LocalizeDefaultPreset).ToList();
    }

    public static List<string> MergeValues(string current, IEnumerable<FilterPreset> presets, Func<FilterPreset, IEnumerable<string>> selector)
    {
        var merged = ParseValues(current);
        foreach (var value in presets.SelectMany(selector))
        {
            if (!merged.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(value);
            }
        }

        return merged;
    }

    public static List<string> ParseValues(string text)
    {
        return text
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddPart(ICollection<string> parts, string label, IEnumerable<string> values)
    {
        var list = values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        if (list.Count > 0)
        {
            parts.Add($"{label}: {string.Join(", ", list)}");
        }
    }
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
