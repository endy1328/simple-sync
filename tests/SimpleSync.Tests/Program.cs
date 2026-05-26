using SimpleSync;

await Run("reports progress while copying changed files", ReportsProgressWhileCopyingChangedFiles);
await Run("sync without progress callback still copies files", SyncWithoutProgressCallbackStillCopiesFiles);
await Run("copy mode preserves target-only files", CopyModePreservesTargetOnlyFiles);
await Run("mirror mode deletes target-only files", MirrorModeDeletesTargetOnlyFiles);
await Run("extension filter copies only matching files", ExtensionFilterCopiesOnlyMatchingFiles);
await Run("specific file filter copies multiple named files", SpecificFileFilterCopiesMultipleNamedFiles);
await Run("exclude filter wins over include filter", ExcludeFilterWinsOverIncludeFilter);
await Run("nested exclude filter preserves included sibling files", NestedExcludeFilterPreservesIncludedSiblingFiles);
await Run("mirror filter preserves target files outside filter", MirrorFilterPreservesTargetFilesOutsideFilter);
await Run("mirror top folder filter preserves subdirectory target files", MirrorTopFolderFilterPreservesSubdirectoryTargetFiles);
await Run("config round-trips sync filters", ConfigRoundTripsSyncFilters);
await Run("config round-trips filter presets", ConfigRoundTripsFilterPresets);
await Run("filter presets merge selected values without duplicates", FilterPresetsMergeSelectedValuesWithoutDuplicates);
await Run("filter summary shows only all files or filtered", FilterSummaryShowsOnlyAllFilesOrFiltered);
await Run("debug build uses separate single instance mutex", DebugBuildUsesSeparateSingleInstanceMutex);
await Run("debug build labels window title and status", DebugBuildLabelsWindowTitleAndStatus);
await Run("new sync pair starts disabled", NewSyncPairStartsDisabled);
await Run("detects no runnable sync pairs", DetectsNoRunnableSyncPairs);
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

static async Task ExtensionFilterCopiesOnlyMatchingFiles()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "notes.md"), "markdown");
    File.WriteAllText(Path.Combine(workspace.Source, "image.png"), "image");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Copy, Source = workspace.Source, Target = workspace.Target, Extensions = [".md"] }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "notes.md")), "matching extension should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "image.png")), "non-matching extension should not copy");
    Assert(result.ExcludedFiles == 1, "excluded file count should include non-matching extension");
}

static async Task SpecificFileFilterCopiesMultipleNamedFiles()
{
    using var workspace = TestWorkspace.Create();
    Directory.CreateDirectory(Path.Combine(workspace.Source, "docs"));
    File.WriteAllText(Path.Combine(workspace.Source, "README.md"), "readme");
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "setup.md"), "setup");
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "draft.md"), "draft");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair
        {
            Enabled = true,
            Mode = SyncModes.Copy,
            Source = workspace.Source,
            Target = workspace.Target,
            Files = ["README.md", "docs/setup.md"]
        }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "README.md")), "first named file should copy");
    Assert(File.Exists(Path.Combine(workspace.Target, "docs", "setup.md")), "second named file should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "docs", "draft.md")), "unnamed file should not copy");
    Assert(result.ExcludedFiles == 1, "excluded count should include unnamed file");
}

static async Task ExcludeFilterWinsOverIncludeFilter()
{
    using var workspace = TestWorkspace.Create();
    Directory.CreateDirectory(Path.Combine(workspace.Source, "docs"));
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "keep.md"), "keep");
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "skip.tmp"), "skip");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair
        {
            Enabled = true,
            Mode = SyncModes.Copy,
            Source = workspace.Source,
            Target = workspace.Target,
            IncludePatterns = ["docs/**"],
            ExcludePatterns = ["*.tmp"]
        }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "docs", "keep.md")), "included non-excluded file should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "docs", "skip.tmp")), "excluded file should not copy");
    Assert(result.ExcludedFiles == 1, "excluded count should include excluded file");
}

static async Task NestedExcludeFilterPreservesIncludedSiblingFiles()
{
    using var workspace = TestWorkspace.Create();
    Directory.CreateDirectory(Path.Combine(workspace.Source, "docs", "private"));
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "readme.md"), "readme");
    File.WriteAllText(Path.Combine(workspace.Source, "docs", "private", "secret.md"), "secret");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair
        {
            Enabled = true,
            Mode = SyncModes.Copy,
            Source = workspace.Source,
            Target = workspace.Target,
            IncludePatterns = ["docs/**"],
            ExcludePatterns = ["docs/private/**"]
        }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "docs", "readme.md")), "included sibling file should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "docs", "private", "secret.md")), "nested excluded file should not copy");
    Assert(result.ExcludedFiles == 0, "excluded nested directory should not count as a per-file exclusion");
}

static async Task MirrorFilterPreservesTargetFilesOutsideFilter()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "keep.md"), "keep");
    File.WriteAllText(Path.Combine(workspace.Target, "old.md"), "old");
    File.WriteAllText(Path.Combine(workspace.Target, "photo.png"), "photo");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair { Enabled = true, Mode = SyncModes.Mirror, Source = workspace.Source, Target = workspace.Target, Extensions = [".md"] }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "keep.md")), "matching source file should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "old.md")), "matching target-only file should be deleted");
    Assert(File.Exists(Path.Combine(workspace.Target, "photo.png")), "outside-filter target file should be preserved");
    Assert(result.DeletedFiles == 1, "only matching target-only file should be deleted");
}

static async Task MirrorTopFolderFilterPreservesSubdirectoryTargetFiles()
{
    using var workspace = TestWorkspace.Create();
    File.WriteAllText(Path.Combine(workspace.Source, "keep.md"), "keep");
    Directory.CreateDirectory(Path.Combine(workspace.Target, "sub"));
    File.WriteAllText(Path.Combine(workspace.Target, "old.md"), "old");
    File.WriteAllText(Path.Combine(workspace.Target, "sub", "nested.md"), "nested");

    var service = new SyncService();
    var result = await service.SyncAsync(
        [new SyncPair
        {
            Enabled = true,
            Mode = SyncModes.Mirror,
            Source = workspace.Source,
            Target = workspace.Target,
            Extensions = [".md"],
            IncludeSubdirectories = false
        }],
        null,
        CancellationToken.None);

    Assert(File.Exists(Path.Combine(workspace.Target, "keep.md")), "matching root source file should copy");
    Assert(!File.Exists(Path.Combine(workspace.Target, "old.md")), "matching root target-only file should be deleted");
    Assert(File.Exists(Path.Combine(workspace.Target, "sub", "nested.md")), "subdirectory target file should be preserved when subdirectories are off");
    Assert(Directory.Exists(Path.Combine(workspace.Target, "sub")), "subdirectory should be preserved when subdirectories are off");
    Assert(result.DeletedFiles == 1, "only matching root target-only file should be deleted");
}

static Task ConfigRoundTripsSyncFilters()
{
    using var workspace = TestWorkspace.Create();
    var configPath = Path.Combine(workspace.Root, "config.toml");
    var service = new ConfigService(configPath);
    service.Save(new AppConfig
    {
        IntervalSeconds = 15,
        Pairs =
        [
            new SyncPair
            {
                Name = "Filtered",
                Enabled = true,
                Mode = SyncModes.Copy,
                Source = @"C:\source",
                Target = @"D:\target",
                IncludePatterns = ["docs/**", "**/*.md"],
                ExcludePatterns = ["bin/**", "*.tmp"],
                Extensions = [".pdf", "docx"],
                Files = ["README.md", "docs/setup.md"],
                IncludeSubdirectories = false
            }
        ]
    });

    var loaded = service.Load();
    var pair = loaded.Pairs.Single();
    Assert(pair.IncludePatterns.SequenceEqual(["docs/**", "**/*.md"]), "include patterns should round-trip");
    Assert(pair.ExcludePatterns.SequenceEqual(["bin/**", "*.tmp"]), "exclude patterns should round-trip");
    Assert(pair.Extensions.SequenceEqual([".pdf", "docx"]), "extensions should round-trip");
    Assert(pair.Files.SequenceEqual(["README.md", "docs/setup.md"]), "files should round-trip");
    Assert(!pair.IncludeSubdirectories, "include_subdirectories should round-trip");

    return Task.CompletedTask;
}

static Task ConfigRoundTripsFilterPresets()
{
    using var workspace = TestWorkspace.Create();
    var configPath = Path.Combine(workspace.Root, "config.toml");
    var service = new ConfigService(configPath);
    service.Save(new AppConfig
    {
        IntervalSeconds = 15,
        FilterPresets =
        [
            new FilterPreset
            {
                Name = "Docs",
                Extensions = [".docx", ".md"],
                Files = ["README.md"],
                IncludePatterns = ["docs/**"],
                ExcludePatterns = ["docs/private/**"]
            }
        ]
    });

    var loaded = service.Load();
    var preset = loaded.FilterPresets.Single();
    Assert(preset.Name == "Docs", "filter preset name should round-trip");
    Assert(preset.Extensions.SequenceEqual([".docx", ".md"]), "filter preset extensions should round-trip");
    Assert(preset.Files.SequenceEqual(["README.md"]), "filter preset files should round-trip");
    Assert(preset.IncludePatterns.SequenceEqual(["docs/**"]), "filter preset include patterns should round-trip");
    Assert(preset.ExcludePatterns.SequenceEqual(["docs/private/**"]), "filter preset exclude patterns should round-trip");

    return Task.CompletedTask;
}

static Task FilterPresetsMergeSelectedValuesWithoutDuplicates()
{
    var documents = new FilterPreset { Name = "Documents", Extensions = [".docx", ".xlsx", ".pdf", ".txt", ".md"] };
    var images = new FilterPreset { Name = "Images", Extensions = [".png", ".jpg", ".svg"] };
    var code = new FilterPreset { Name = "Code", Extensions = [".cs", ".js", ".json", ".xml", ".md"] };
    var merged = FilterPreset.MergeValues(".docx", [documents, images, code, documents], preset => preset.Extensions);

    Assert(
        merged.SequenceEqual([".docx", ".xlsx", ".pdf", ".txt", ".md", ".png", ".jpg", ".svg", ".cs", ".js", ".json", ".xml"]),
        "selected preset values should append in click order and avoid duplicates");

    return Task.CompletedTask;
}

static Task FilterSummaryShowsOnlyAllFilesOrFiltered()
{
    Assert(
        SyncFilter.FromPair(new SyncPair()).Summary == "All files",
        "empty filter should summarize as all files");

    Assert(
        SyncFilter.FromPair(new SyncPair { Extensions = [".docx", ".xlsx", ".pdf"] }).Summary == "Filtered",
        "extension filter should summarize as filtered");

    Assert(
        SyncFilter.FromPair(new SyncPair { IncludeSubdirectories = false }).Summary == "Filtered",
        "top-folder-only filter should summarize as filtered");

    var detail = SyncFilter.FromPair(new SyncPair { Extensions = [".docx", ".xlsx"] }).Detail;
    Assert(detail.Contains(".docx", StringComparison.OrdinalIgnoreCase), "filter detail should keep full values for tooltip");

    return Task.CompletedTask;
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

static Task DebugBuildLabelsWindowTitleAndStatus()
{
    Assert(
        SimpleSync.MainForm.FormatWindowTitle(isDebugBuild: false) == "simple sync",
        "release title should not include debug label");

    Assert(
        SimpleSync.MainForm.FormatWindowTitle(isDebugBuild: true) == "simple sync (Debug)",
        "debug title should include debug label");

    Assert(
        SimpleSync.MainForm.FormatStatus("1.1.0", "Ready", isDebugBuild: false) == "simple sync 1.1.0 | Ready",
        "release status should not include debug label");

    Assert(
        SimpleSync.MainForm.FormatStatus("1.1.0", "Ready", isDebugBuild: true) == "simple sync 1.1.0 Debug | Ready",
        "debug status should include debug label");

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

static Task DetectsNoRunnableSyncPairs()
{
    var pairs = new[]
    {
        new SyncPair { Name = "Pair 1", Enabled = false, Source = @"C:\source", Target = @"D:\target" },
        new SyncPair { Name = "Pair 2", Enabled = false, Source = @"C:\source2", Target = @"D:\target2" }
    };

    Assert(SimpleSync.MainForm.GetRunnablePairs(pairs).Count == 0, "disabled pairs should not be runnable");
    Assert(SimpleSync.MainForm.NoSyncTargetsMessage == "대상이 없습니다.", "no target message should be explicit");

    return Task.CompletedTask;
}

static Task FormatsSelectedPairCurrentStatus()
{
    var pair = new SyncPair { Name = "Docs", Source = @"C:\source", Target = @"D:\target" };

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, null) == "Selected: Docs - Idle",
        "missing progress should show idle status");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Preparing }) == "Selected: Docs - Scanning",
        "preparing should show scanning status");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Copying, CurrentPath = "a.txt" }) == "Selected: Docs - Copying",
        "copying header status should stay compact");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Deleting, CurrentPath = "old.txt" }) == "Selected: Docs - Deleting",
        "deleting header status should stay compact");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Completed }) == "Selected: Docs - Done",
        "completed should show done status");

    Assert(
        SimpleSync.MainForm.FormatCurrentDetail(new SyncProgress { Pair = pair, Phase = SyncPhase.Copying, CurrentPath = "a.txt" }) == "a.txt",
        "progress tooltip detail should include current relative path");

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
