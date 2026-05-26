using System.Text.RegularExpressions;

namespace SimpleSync;

public sealed class SyncFilter
{
    private readonly bool _includeSubdirectories;
    private readonly List<string> _includePatterns;
    private readonly List<string> _excludePatterns;
    private readonly HashSet<string> _extensions;
    private readonly HashSet<string> _files;

    private SyncFilter(SyncPair pair)
    {
        _includeSubdirectories = pair.IncludeSubdirectories;
        _includePatterns = NormalizePatterns(pair.IncludePatterns ?? []);
        _excludePatterns = NormalizePatterns(pair.ExcludePatterns ?? []);
        _extensions = (pair.Extensions ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _files = (pair.Files ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static SyncFilter FromPair(SyncPair pair)
    {
        return new SyncFilter(pair);
    }

    public bool HasRules =>
        _includePatterns.Count > 0 ||
        _excludePatterns.Count > 0 ||
        _extensions.Count > 0 ||
        _files.Count > 0 ||
        !_includeSubdirectories;

    public string Summary
    {
        get
        {
            return HasRules ? "Filtered" : "All files";
        }
    }

    public string Detail
    {
        get
        {
            if (!HasRules)
            {
                return "All files";
            }

            var parts = new List<string>();
            AddDetail(parts, "Include", _includePatterns);
            AddDetail(parts, "Extensions", _extensions);
            AddDetail(parts, "Files", _files);
            AddDetail(parts, "Exclude", _excludePatterns);
            parts.Add($"Subdirectories: {(_includeSubdirectories ? "yes" : "no")}");
            return string.Join(Environment.NewLine, parts);
        }
    }

    public bool ShouldTraverseDirectory(string relativeDirectory)
    {
        var normalized = NormalizeRelativePath(relativeDirectory);
        if (normalized.Length == 0)
        {
            return true;
        }

        if (!_includeSubdirectories)
        {
            return false;
        }

        return !MatchesAnyDirectory(_excludePatterns, normalized);
    }

    public bool ShouldCopyFile(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (normalized.EndsWith(".simple-sync.tmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MatchesAny(_excludePatterns, normalized))
        {
            return false;
        }

        var hasAllowRules = _includePatterns.Count > 0 || _extensions.Count > 0 || _files.Count > 0;
        if (!hasAllowRules)
        {
            return true;
        }

        return MatchesAny(_includePatterns, normalized) ||
               _extensions.Contains(NormalizeExtension(Path.GetExtension(normalized))) ||
               _files.Contains(normalized);
    }

    public bool ShouldDeleteTargetFile(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (!_includeSubdirectories && normalized.Contains('/'))
        {
            return false;
        }

        return ShouldCopyFile(normalized);
    }

    public bool ShouldDeleteTargetDirectory(string relativeDirectory)
    {
        return ShouldTraverseDirectory(relativeDirectory);
    }

    private static void AddDetail(ICollection<string> parts, string label, IEnumerable<string> values)
    {
        var list = values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        if (list.Count > 0)
        {
            parts.Add($"{label}: {string.Join(", ", list)}");
        }
    }

    private static List<string> NormalizePatterns(IEnumerable<string> patterns)
    {
        return patterns
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeRelativePath)
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/').Trim('/');
        return normalized == "." ? string.Empty : normalized;
    }

    private static string NormalizeExtension(string extension)
    {
        var normalized = extension.Trim();
        if (normalized.Length == 0)
        {
            return normalized;
        }

        return normalized.StartsWith('.') ? normalized : "." + normalized;
    }

    private static bool MatchesAny(IEnumerable<string> patterns, string normalizedPath)
    {
        return patterns.Any(pattern => GlobMatches(pattern, normalizedPath));
    }

    private static bool MatchesAnyDirectory(IEnumerable<string> patterns, string normalizedDirectory)
    {
        return patterns.Any(pattern =>
            GlobMatches(pattern, normalizedDirectory) ||
            GlobMatches(pattern, normalizedDirectory + "/"));
    }

    private static bool GlobMatches(string pattern, string normalizedPath)
    {
        if (pattern.StartsWith("**/", StringComparison.Ordinal))
        {
            var tail = pattern[3..];
            if (GlobMatches(tail, normalizedPath))
            {
                return true;
            }
        }

        if (!pattern.Contains('/'))
        {
            var fileName = Path.GetFileName(normalizedPath);
            return Regex.IsMatch(fileName, GlobToRegex(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return Regex.IsMatch(normalizedPath, GlobToRegex(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GlobToRegex(string pattern)
    {
        var regex = "^";
        for (var i = 0; i < pattern.Length; i++)
        {
            var current = pattern[i];
            if (current == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    regex += ".*";
                    i++;
                }
                else
                {
                    regex += "[^/]*";
                }
            }
            else if (current == '?')
            {
                regex += "[^/]";
            }
            else
            {
                regex += Regex.Escape(current.ToString());
            }
        }

        return regex + "$";
    }
}
