using SimpleSync;

await Run("reports progress while copying changed files", ReportsProgressWhileCopyingChangedFiles);
await Run("sync without progress callback still copies files", SyncWithoutProgressCallbackStillCopiesFiles);
await Run("copy mode preserves target-only files", CopyModePreservesTargetOnlyFiles);
await Run("mirror mode deletes target-only files", MirrorModeDeletesTargetOnlyFiles);

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
