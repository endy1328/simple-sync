namespace SimpleSync;

public sealed class SyncService
{
    public Task<SyncResult> SyncAsync(IEnumerable<SyncPair> pairs, CancellationToken cancellationToken)
    {
        return Task.Run(() => Sync(pairs, cancellationToken), cancellationToken);
    }

    private static SyncResult Sync(IEnumerable<SyncPair> pairs, CancellationToken cancellationToken)
    {
        var result = new SyncResult();

        foreach (var pair in pairs.Where(pair => pair.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = pair.Source ?? string.Empty;
            var target = pair.Target ?? string.Empty;

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                result.Messages.Add("소스 또는 타겟 경로가 비어 있어 건너뜀");
                continue;
            }

            string sourceRoot;
            string targetRoot;

            try
            {
                sourceRoot = Path.GetFullPath(source);
                targetRoot = Path.GetFullPath(target);
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"경로 해석 실패: {ex.Message}");
                continue;
            }

            if (!Directory.Exists(sourceRoot))
            {
                result.Messages.Add($"소스 없음: {source}");
                continue;
            }

            if (IsSameOrChildPath(sourceRoot, targetRoot))
            {
                result.Messages.Add($"타겟이 소스와 같거나 소스 내부에 있어 건너뜀: {target}");
                continue;
            }

            try
            {
                Directory.CreateDirectory(targetRoot);
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"타겟 생성 실패: {targetRoot} - {ex.Message}");
                continue;
            }

            var failuresBeforePair = result.FailedFiles;
            CopyDirectory(sourceRoot, targetRoot, result, cancellationToken);

            if (SyncModes.Normalize(pair.Mode) != SyncModes.Mirror)
            {
                continue;
            }

            if (result.FailedFiles != failuresBeforePair)
            {
                result.Messages.Add("복사 중 실패가 있어 mirror 삭제 단계를 건너뜀");
                continue;
            }

            DeleteTargetExtras(sourceRoot, targetRoot, result, cancellationToken);
        }

        return result;
    }

    private static void CopyDirectory(string sourceRoot, string targetRoot, SyncResult result, CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(sourceRoot);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDirectory = directories.Pop();
            List<string> sourceFiles;
            List<string> childDirectories;

            try
            {
                var relativeDirectory = Path.GetRelativePath(sourceRoot, currentDirectory);
                var targetDirectory = relativeDirectory == "."
                    ? targetRoot
                    : Path.Combine(targetRoot, relativeDirectory);
                Directory.CreateDirectory(targetDirectory);

                sourceFiles = Directory.EnumerateFiles(currentDirectory).ToList();
                childDirectories = Directory.EnumerateDirectories(currentDirectory).ToList();
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"{Path.GetRelativePath(sourceRoot, currentDirectory)}: {ex.Message}");
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                directories.Push(childDirectory);
            }

            foreach (var sourceFile in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
                var targetFile = Path.Combine(targetRoot, relativePath);

                try
                {
                    if (!NeedsCopy(sourceFile, targetFile))
                    {
                        result.SkippedFiles++;
                        continue;
                    }

                    var targetDirectory = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(sourceFile, targetFile, overwrite: true);
                    File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
                    result.CopiedFiles++;
                }
                catch (Exception ex) when (IsFileSystemException(ex))
                {
                    result.FailedFiles++;
                    result.Messages.Add($"{relativePath}: {ex.Message}");
                }
            }
        }
    }

    private static void DeleteTargetExtras(string sourceRoot, string targetRoot, SyncResult result, CancellationToken cancellationToken)
    {
        List<string> targetFiles;
        List<string> targetDirectories;

        try
        {
            targetFiles = Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories).ToList();
            targetDirectories = Directory.EnumerateDirectories(targetRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToList();
        }
        catch (Exception ex) when (IsFileSystemException(ex))
        {
            result.FailedFiles++;
            result.Messages.Add($"mirror 대상 탐색 실패: {ex.Message}");
            return;
        }

        foreach (var targetFile in targetFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(targetRoot, targetFile);
            var sourceFile = Path.Combine(sourceRoot, relativePath);
            if (File.Exists(sourceFile))
            {
                continue;
            }

            try
            {
                File.Delete(targetFile);
                result.DeletedFiles++;
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"{relativePath}: 삭제 실패 - {ex.Message}");
            }
        }

        foreach (var targetDirectory in targetDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(targetRoot, targetDirectory);
            var sourceDirectory = Path.Combine(sourceRoot, relativePath);
            if (Directory.Exists(sourceDirectory))
            {
                continue;
            }

            try
            {
                Directory.Delete(targetDirectory, recursive: true);
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"{relativePath}: 디렉터리 삭제 실패 - {ex.Message}");
            }
        }
    }

    private static bool NeedsCopy(string sourceFile, string targetFile)
    {
        if (!File.Exists(targetFile))
        {
            return true;
        }

        var sourceInfo = new FileInfo(sourceFile);
        var targetInfo = new FileInfo(targetFile);

        return sourceInfo.Length != targetInfo.Length ||
               Math.Abs((sourceInfo.LastWriteTimeUtc - targetInfo.LastWriteTimeUtc).TotalSeconds) >= 1;
    }

    private static bool IsSameOrChildPath(string parentPath, string candidatePath)
    {
        var parent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));

        return candidate.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }

    private static bool IsFileSystemException(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or ArgumentException or NotSupportedException;
    }
}
