using System.Text;

namespace SimpleSync;

public sealed class ConfigService
{
    private readonly string _configPath;

    public ConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig();
        }

        var config = new AppConfig();
        SyncPair? currentPair = null;
        FilterPreset? currentPreset = null;
        var loadedFilterPresets = false;

        foreach (var rawLine in File.ReadAllLines(_configPath, Encoding.UTF8))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("[[pairs]]", StringComparison.OrdinalIgnoreCase))
            {
                currentPreset = null;
                currentPair = new SyncPair();
                config.Pairs.Add(currentPair);
                continue;
            }

            if (line.Equals("[[filter_presets]]", StringComparison.OrdinalIgnoreCase))
            {
                currentPair = null;
                if (!loadedFilterPresets)
                {
                    config.FilterPresets.Clear();
                    loadedFilterPresets = true;
                }

                currentPreset = new FilterPreset();
                config.FilterPresets.Add(currentPreset);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (currentPair is null && currentPreset is null)
            {
                if (key.Equals("interval_seconds", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(value, out var seconds))
                {
                    config.IntervalSeconds = Math.Clamp(seconds, 1, 86_400);
                }
                else if (key.Equals("skin", StringComparison.OrdinalIgnoreCase))
                {
                    config.Skin = Unquote(value);
                }
                else if (key.Equals("language", StringComparison.OrdinalIgnoreCase))
                {
                    config.Language = LocalizationService.NormalizeLanguage(Unquote(value));
                }
                else if (key.Equals("window_width", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(value, out var width))
                {
                    config.WindowWidth = Math.Clamp(width, 860, 10_000);
                }
                else if (key.Equals("window_height", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(value, out var height))
                {
                    config.WindowHeight = Math.Clamp(height, 560, 10_000);
                }
                continue;
            }

            if (currentPreset is not null)
            {
                if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    currentPreset.Name = Unquote(value);
                }
                else if (key.Equals("extensions", StringComparison.OrdinalIgnoreCase))
                {
                    currentPreset.Extensions = ParseStringArray(value);
                }
                else if (key.Equals("files", StringComparison.OrdinalIgnoreCase))
                {
                    currentPreset.Files = ParseStringArray(value);
                }
                else if (key.Equals("include", StringComparison.OrdinalIgnoreCase))
                {
                    currentPreset.IncludePatterns = ParseStringArray(value);
                }
                else if (key.Equals("exclude", StringComparison.OrdinalIgnoreCase))
                {
                    currentPreset.ExcludePatterns = ParseStringArray(value);
                }

                continue;
            }

            if (currentPair is null)
            {
                continue;
            }

            if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(value, out var enabled))
            {
                currentPair.Enabled = enabled;
            }
            else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Name = Unquote(value);
            }
            else if (key.Equals("mode", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Mode = SyncModes.Normalize(Unquote(value));
            }
            else if (key.Equals("source", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Source = Unquote(value);
            }
            else if (key.Equals("target", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Target = Unquote(value);
            }
            else if (key.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.IncludePatterns = ParseStringArray(value);
            }
            else if (key.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.ExcludePatterns = ParseStringArray(value);
            }
            else if (key.Equals("extensions", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Extensions = ParseStringArray(value);
            }
            else if (key.Equals("files", StringComparison.OrdinalIgnoreCase))
            {
                currentPair.Files = ParseStringArray(value);
            }
            else if (key.Equals("include_subdirectories", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(value, out var includeSubdirectories))
            {
                currentPair.IncludeSubdirectories = includeSubdirectories;
            }
        }

        return config;
    }

    public void Save(AppConfig config)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"interval_seconds = {Math.Clamp(config.IntervalSeconds, 1, 86_400)}");
        builder.AppendLine($"skin = \"{Escape(config.Skin)}\"");
        builder.AppendLine($"language = \"{Escape(LocalizationService.NormalizeLanguage(config.Language))}\"");
        builder.AppendLine($"window_width = {Math.Clamp(config.WindowWidth, 860, 10_000)}");
        builder.AppendLine($"window_height = {Math.Clamp(config.WindowHeight, 560, 10_000)}");
        builder.AppendLine();

        foreach (var preset in NormalizeFilterPresets(config.FilterPresets))
        {
            builder.AppendLine("[[filter_presets]]");
            builder.AppendLine($"name = \"{Escape(preset.Name)}\"");
            AppendArray(builder, "extensions", preset.Extensions);
            AppendArray(builder, "files", preset.Files);
            AppendArray(builder, "include", preset.IncludePatterns);
            AppendArray(builder, "exclude", preset.ExcludePatterns);
            builder.AppendLine();
        }

        foreach (var pair in config.Pairs)
        {
            builder.AppendLine("[[pairs]]");
            builder.AppendLine($"name = \"{Escape(pair.Name)}\"");
            builder.AppendLine($"enabled = {pair.Enabled.ToString().ToLowerInvariant()}");
            builder.AppendLine($"mode = \"{Escape(SyncModes.Normalize(pair.Mode))}\"");
            builder.AppendLine($"source = \"{Escape(pair.Source)}\"");
            builder.AppendLine($"target = \"{Escape(pair.Target)}\"");
            AppendArray(builder, "include", pair.IncludePatterns);
            AppendArray(builder, "exclude", pair.ExcludePatterns);
            AppendArray(builder, "extensions", pair.Extensions);
            AppendArray(builder, "files", pair.Files);
            if (!pair.IncludeSubdirectories)
            {
                builder.AppendLine("include_subdirectories = false");
            }

            builder.AppendLine();
        }

        File.WriteAllText(_configPath, builder.ToString(), Encoding.UTF8);
    }

    private static List<FilterPreset> NormalizeFilterPresets(IEnumerable<FilterPreset>? presets)
    {
        var normalized = (presets ?? [])
            .Where(preset => !string.IsNullOrWhiteSpace(preset.Name))
            .Select(preset => new FilterPreset
            {
                Name = preset.Name.Trim(),
                Extensions = CleanValues(preset.Extensions),
                Files = CleanValues(preset.Files),
                IncludePatterns = CleanValues(preset.IncludePatterns),
                ExcludePatterns = CleanValues(preset.ExcludePatterns)
            })
            .ToList();

        return normalized.Count == 0 ? FilterPreset.CreateDefaults() : normalized;
    }

    private static List<string> CleanValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (line[i] == '#' && !inString)
            {
                return line[..i];
            }
        }

        return line;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static List<string> ParseStringArray(string value)
    {
        value = value.Trim();
        if (value.Length < 2 || value[0] != '[' || value[^1] != ']')
        {
            return [];
        }

        var items = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var escaped = false;

        foreach (var ch in value[1..^1])
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                if (inString)
                {
                    items.Add(current.ToString());
                    current.Clear();
                }

                inString = !inString;
                continue;
            }

            if (inString)
            {
                current.Append(ch);
            }
        }

        return items;
    }

    private static void AppendArray(StringBuilder builder, string key, IEnumerable<string>? values)
    {
        var items = (values ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        if (items.Count == 0)
        {
            return;
        }

        builder.Append(key);
        builder.Append(" = [");
        builder.Append(string.Join(", ", items.Select(item => $"\"{Escape(item)}\"")));
        builder.AppendLine("]");
    }
}
