using SimpleSync;

await Run("reports progress while copying changed files", ReportsProgressWhileCopyingChangedFiles);
await Run("sync without progress callback still copies files", SyncWithoutProgressCallbackStillCopiesFiles);
await Run("copy mode preserves target-only files", CopyModePreservesTargetOnlyFiles);
await Run("mirror mode deletes target-only files", MirrorModeDeletesTargetOnlyFiles);
await Run("debug build uses separate single instance mutex", DebugBuildUsesSeparateSingleInstanceMutex);
await Run("new sync pair starts disabled", NewSyncPairStartsDisabled);
await Run("formats selected pair current status", FormatsSelectedPairCurrentStatus);

static async Task Run(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

static async Task ReportsProgressWhileCopyingChangedFiles()
{
    using var workspace = TestWorkspace.Create();
    var sourceFile = Path.Combine(workspace.Source, "large.bin");
    File.WriteAllBytes(sourceFile, Enumerable.Range(0, 2 * 1024 * 1024).Select(i => (byte)(i % 251)).ToArray());

    var progressEvents = new List<SyncProgress>();
    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Copy, Source = workspace.Source, Target = workspace.Target }],
        new Progress<SyncProgress>(progressEvents.Add),
        CancellationToken.None);

    Assert(result.FailedFiles == 0, "copy should not fail");
    Assert(File.Exists(Path.Combine(workspace.Target, "large.bin")), "target file should exist");
    Assert(progressEvents.Any(item => item.Phase == SyncPhase.Copying && item.CurrentPath == "large.bin"), "copy progress should include current file");
    Assert(progressEvents.Any(item => item.CurrentFileBytes > 0 && item.CurrentFileTotalBytes == 2 * 1024 * 1024), "copy progress should include byte counts");
    Assert(progressEvents.Any(item => item.Phase == SyncPhase.Completed && item.ProcessedFiles == item.TotalFiles), "progress should complete");
}

static async Task CopyModePreservesTargetOnlyFiles()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "source.txt"), "source");
    File.WriteAllText(Path.Combine(workspace.Target, "target-only.txt"), "target");

    var service = new SyncService();
    await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Copy, Source = workspace.Source, Target = workspace.Target }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "target-only.txt")), "copy mode should preserve target-only file");
}

static async Task SyncWithoutProgressCallbackStillCopiesFiles()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "plain.txt"), "plain");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Copy, Source = workspace.Source, Target = workspace.Target }],
        CancellationToken.None);

    Assert(result.FailedFiles == 0, "copy without progress should not fail");
    Assert(File.Exists(Path.Combine(workspace.Target, "plain.txt")), "copy without progress should copy file");
}

static async Task MirrorModeDeletesTargetOnlyFiles()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "source.txt"), "source");
    File.WriteAllText(Path.Combine(workspace.Target, "target-only.txt"), "target");

    var service = new SyncService();
    await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Mirror, Source = workspace.Source, Target = workspace.Target }],
        null,
        CancellationToken.None);

    Assert(!File.Exists(Path.Combine(workspace.Target, "target-only.txt")), "mirror mode should delete target-only file");
}

static Task DebugBuildUsesSeparateSingleInstanceMutex()
{
    var releaseMutex = SimpleSync.Program.GetSingleInstanceMutexName(isDebugBuild: false);
    var debugMutex = SimpleSync.Program.GetSingleInstanceMutexName(isDebugBuild: true);

    Assert(releaseMutex.Length > 0, "release mutex should not be empty");
    Assert(debugMutex.Length > 0, "debug mutex should not be empty");
    Assert(releaseMutex != debugMutex, "debug and release mutex names should be different");
    Assert(debugMutex.EndsWith("_Debug", StringComparison.Ordinal), "debug mutex should be clearly marked");

    return Task.CompletedTask;
}

static Task NewSyncPairStartsDisabled()
{
    var pair = SimpleSync.MainForm.CreateNewPair(3);

    Assert(pair.Name == "Pair 3", "new pair should use the requested display number");
    Assert(!pair.Enabled, "new pair should start disabled until paths are ready");
    Assert(pair.Mode == SyncModes.Copy, "new pair should keep copy mode as default");
    Assert(pair.Source == string.Empty, "new pair source should start empty");
    Assert(pair.Target == string.Empty, "new pair target should start empty");

    return Task.CompletedTask;
}

static Task FormatsSelectedPairCurrentStatus()
{
    var pair = new SyncPair { Name = "Docs", Source = @"C:\source", Target = @"D:\target" };

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, null) == "Docs: idle",
        "missing progress should show idle status");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Preparing }) == "Docs: Scanning files",
        "preparing should show scanning status");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Copying, CurrentPath = "a.txt" }) == "Docs: Copying a.txt",
        "copying should include current relative path");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Deleting, CurrentPath = "old.txt" }) == "Docs: Deleting old.txt",
        "deleting should include current relative path");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Completed }) == "Docs: Done",
        "completed should show done status");

    return Task.CompletedTask;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class TestWorkspace : IDisposable
{
    public string Root { get; }
    public string Source { get; }
    public string Target { get; }

    private TestWorkspace(string root)
    {
        Root = root;
        Source = Path.Combine(root, "source");
        Target = Path.Combine(root, "target");
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Target);
    }

    public static TestWorkspace Create()
    {
        return new TestWorkspace(Path.Combine(Path.GetTempPath(), "simple-sync-test-" + Guid.NewGuid().ToString("N")));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
