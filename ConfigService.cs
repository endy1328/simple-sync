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

        foreach (var rawLine in File.ReadAllLines(_configPath, Encoding.UTF8))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("[[pairs]]", StringComparison.OrdinalIgnoreCase))
            {
                currentPair = new SyncPair();
                config.Pairs.Add(currentPair);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (currentPair is null)
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
        }

        return config;
    }

    public void Save(AppConfig config)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"interval_seconds = {Math.Clamp(config.IntervalSeconds, 1, 86_400)}");
        builder.AppendLine($"skin = \"{Escape(config.Skin)}\"");
        builder.AppendLine($"window_width = {Math.Clamp(config.WindowWidth, 860, 10_000)}");
        builder.AppendLine($"window_height = {Math.Clamp(config.WindowHeight, 560, 10_000)}");
        builder.AppendLine();

        foreach (var pair in config.Pairs)
        {
            builder.AppendLine("[[pairs]]");
            builder.AppendLine($"name = \"{Escape(pair.Name)}\"");
            builder.AppendLine($"enabled = {pair.Enabled.ToString().ToLowerInvariant()}");
            builder.AppendLine($"mode = \"{Escape(SyncModes.Normalize(pair.Mode))}\"");
            builder.AppendLine($"source = \"{Escape(pair.Source)}\"");
            builder.AppendLine($"target = \"{Escape(pair.Target)}\"");
            builder.AppendLine();
        }

        File.WriteAllText(_configPath, builder.ToString(), Encoding.UTF8);
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
}
