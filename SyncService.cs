namespace SimpleSync;

public sealed class SyncService
{
    private const int CopyBufferSize = 1024 * 1024;
    private const int MaxFileCopyAttempts = 3;

    public Task<SyncResult> SyncAsync(IEnumerable<SyncPair> pairs, CancellationToken cancellationToken)
    {
        return SyncAsync(pairs, progress: null, cancellationToken);
    }

    public Task<SyncResult> SyncAsync(IEnumerable<SyncPair> pairs, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() => Sync(pairs, progress, cancellationToken), cancellationToken);
    }

    private static SyncResult Sync(IEnumerable<SyncPair> pairs, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
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
            var totalFiles = progress is null
                ? null
                : CountSourceFiles(pair, sourceRoot, result, progress, cancellationToken);
            var progressState = new SyncProgressState(pair, totalFiles);
            ReportProgress(progress, progressState, SyncPhase.Preparing);

            CopyDirectory(sourceRoot, targetRoot, result, progressState, progress, cancellationToken);

            if (SyncModes.Normalize(pair.Mode) != SyncModes.Mirror)
            {
                ReportProgress(progress, progressState, result.FailedFiles == failuresBeforePair ? SyncPhase.Completed : SyncPhase.Failed);
                continue;
            }

            if (result.FailedFiles != failuresBeforePair)
            {
                result.Messages.Add("복사 중 실패가 있어 mirror 삭제 단계를 건너뜀");
                progressState.FailedFiles = result.FailedFiles;
                ReportProgress(progress, progressState, SyncPhase.Failed, message: "복사 중 실패가 있어 mirror 삭제 단계를 건너뜀");
                continue;
            }

            DeleteTargetExtras(sourceRoot, targetRoot, result, progressState, progress, cancellationToken);
            ReportProgress(progress, progressState, result.FailedFiles == failuresBeforePair ? SyncPhase.Completed : SyncPhase.Failed);
        }

        return result;
    }

    private static void CopyDirectory(
        string sourceRoot,
        string targetRoot,
        SyncResult result,
        SyncProgressState progressState,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
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
                    progressState.CurrentPath = relativePath;
                    progressState.CurrentFileBytes = 0;
                    progressState.CurrentFileTotalBytes = null;

                    if (!NeedsCopy(sourceFile, targetFile))
                    {
                        result.SkippedFiles++;
                        progressState.SkippedFiles++;
                        progressState.ProcessedFiles++;
                        ReportProgress(progress, progressState, SyncPhase.Copying);
                        continue;
                    }

                    var targetDirectory = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    CopyFileWithRetries(sourceFile, targetFile, relativePath, progressState, progress, cancellationToken);
                    result.CopiedFiles++;
                    progressState.CopiedFiles++;
                    progressState.ProcessedFiles++;
                    ReportProgress(progress, progressState, SyncPhase.Copying);
                }
                catch (Exception ex) when (IsFileSystemException(ex))
                {
                    result.FailedFiles++;
                    progressState.FailedFiles++;
                    progressState.ProcessedFiles++;
                    result.Messages.Add($"{relativePath}: {ex.Message}");
                    ReportProgress(progress, progressState, SyncPhase.Failed, $"{relativePath}: {ex.Message}");
                }
            }
        }
    }

    private static void DeleteTargetExtras(
        string sourceRoot,
        string targetRoot,
        SyncResult result,
        SyncProgressState progressState,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
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
            progressState.FailedFiles++;
            result.Messages.Add($"mirror 대상 탐색 실패: {ex.Message}");
            ReportProgress(progress, progressState, SyncPhase.Failed, $"mirror 대상 탐색 실패: {ex.Message}");
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
                progressState.CurrentPath = relativePath;
                ReportProgress(progress, progressState, SyncPhase.Deleting);
                File.Delete(targetFile);
                result.DeletedFiles++;
                progressState.DeletedFiles++;
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                progressState.FailedFiles++;
                result.Messages.Add($"{relativePath}: 삭제 실패 - {ex.Message}");
                ReportProgress(progress, progressState, SyncPhase.Failed, $"{relativePath}: 삭제 실패 - {ex.Message}");
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
                progressState.CurrentPath = relativePath;
                ReportProgress(progress, progressState, SyncPhase.Deleting);
                Directory.Delete(targetDirectory, recursive: true);
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                progressState.FailedFiles++;
                result.Messages.Add($"{relativePath}: 디렉터리 삭제 실패 - {ex.Message}");
                ReportProgress(progress, progressState, SyncPhase.Failed, $"{relativePath}: 디렉터리 삭제 실패 - {ex.Message}");
            }
        }
    }

    private static int? CountSourceFiles(
        SyncPair pair,
        string sourceRoot,
        SyncResult result,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var count = 0;
        var directories = new Stack<string>();
        directories.Push(sourceRoot);
        var state = new SyncProgressState(pair, totalFiles: null);
        ReportProgress(progress, state, SyncPhase.Preparing, message: "파일 목록 계산 중");

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = directories.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentDirectory))
                {
                    _ = file;
                    count++;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
                {
                    directories.Push(childDirectory);
                }
            }
            catch (Exception ex) when (IsFileSystemException(ex))
            {
                result.FailedFiles++;
                result.Messages.Add($"{Path.GetRelativePath(sourceRoot, currentDirectory)}: {ex.Message}");
                ReportProgress(progress, state, SyncPhase.Failed, $"{Path.GetRelativePath(sourceRoot, currentDirectory)}: {ex.Message}");
            }
        }

        return count;
    }

    private static void CopyFileSafely(
        string sourceFile,
        string targetFile,
        string relativePath,
        SyncProgressState progressState,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempFile = targetFile + ".simple-sync.tmp";
        var sourceInfo = new FileInfo(sourceFile);
        progressState.CurrentPath = relativePath;
        progressState.CurrentFileBytes = 0;
        progressState.CurrentFileTotalBytes = sourceInfo.Length;
        ReportProgress(progress, progressState, SyncPhase.Copying);

        try
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            {
                using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.SequentialScan);
                using var targetStream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.SequentialScan);
                var buffer = new byte[CopyBufferSize];

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = sourceStream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }

                    targetStream.Write(buffer, 0, read);
                    progressState.CurrentFileBytes += read;
                    ReportProgress(progress, progressState, SyncPhase.Copying);
                }

                targetStream.Flush(flushToDisk: true);
            }

            File.SetLastWriteTimeUtc(tempFile, sourceInfo.LastWriteTimeUtc);
            File.Move(tempFile, targetFile, overwrite: true);
            File.SetLastWriteTimeUtc(targetFile, sourceInfo.LastWriteTimeUtc);
        }
        catch
        {
            TryDeleteTempFile(tempFile);
            throw;
        }
    }

    private static void CopyFileWithRetries(
        string sourceFile,
        string targetFile,
        string relativePath,
        SyncProgressState progressState,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxFileCopyAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                CopyFileSafely(sourceFile, targetFile, relativePath, progressState, progress, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxFileCopyAttempts && IsFileSystemException(ex))
            {
                progressState.Message = $"{relativePath}: 재시도 {attempt}/{MaxFileCopyAttempts - 1}";
                ReportProgress(progress, progressState, SyncPhase.Copying, progressState.Message);
                Thread.Sleep(TimeSpan.FromMilliseconds(250 * attempt));
            }
        }
    }

    private static void TryDeleteTempFile(string tempFile)
    {
        try
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception ex) when (IsFileSystemException(ex))
        {
            // Best-effort cleanup only. The original copy failure is more useful to report.
        }
    }

    private static void ReportProgress(
        IProgress<SyncProgress>? progress,
        SyncProgressState state,
        SyncPhase phase,
        string? message = null)
    {
        progress?.Report(new SyncProgress
        {
            Pair = state.Pair,
            Phase = phase,
            ProcessedFiles = state.ProcessedFiles,
            TotalFiles = state.TotalFiles,
            CurrentPath = state.CurrentPath,
            CurrentFileBytes = state.CurrentFileBytes,
            CurrentFileTotalBytes = state.CurrentFileTotalBytes,
            CopiedFiles = state.CopiedFiles,
            SkippedFiles = state.SkippedFiles,
            DeletedFiles = state.DeletedFiles,
            FailedFiles = state.FailedFiles,
            Message = message
        });
    }

    private sealed class SyncProgressState
    {
        public SyncProgressState(SyncPair pair, int? totalFiles)
        {
            Pair = pair;
            TotalFiles = totalFiles;
        }

        public SyncPair Pair { get; }
        public int ProcessedFiles { get; set; }
        public int? TotalFiles { get; }
        public string? CurrentPath { get; set; }
        public long CurrentFileBytes { get; set; }
        public long? CurrentFileTotalBytes { get; set; }
        public string? Message { get; set; }
        public int CopiedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int DeletedFiles { get; set; }
        public int FailedFiles { get; set; }
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
