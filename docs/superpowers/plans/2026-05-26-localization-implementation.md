# simple sync Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Korean and English UI localization to `simple sync`, persist the selected language in `config.toml`, and keep existing sync behavior unchanged.

**Architecture:** Add a small key-based localization layer with internal dictionaries for `ko-KR` and `en-US`. The WinForms UI keeps its current manual-control structure, and `MainForm`/`FilterDialog` apply localized text in place without rebuilding forms.

**Tech Stack:** .NET 10, C# 14, Windows Forms, existing console-style test harness in `tests/SimpleSync.Tests`.

---

## File Structure

- Create `C:\workspace\simple-sync\LocalizationService.cs`: language options, translation dictionaries, text lookup, formatting, mode label helpers, filter preset localization helpers.
- Modify `C:\workspace\simple-sync\Models.cs`: add `AppConfig.Language` with Korean default.
- Modify `C:\workspace\simple-sync\ConfigService.cs`: load and save top-level `language`.
- Modify `C:\workspace\simple-sync\MainForm.cs`: add language selector, apply localized labels, persist selected language, localize new log/status strings, and pass localization into filter dialogs.
- Modify `C:\workspace\simple-sync\FilterDialog.cs`: accept a localization service and localize dialog text, hints, buttons, and fallback preset UI.
- Modify `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`: add focused tests for fallback, config language round-trip, labels, status formatting, and filter preset localization.
- Modify `C:\workspace\simple-sync\config.example.toml`: add `language = "ko-KR"`.
- Modify `C:\workspace\simple-sync\README.md`: Korean-only usage update for language selection and version.
- Modify `C:\workspace\simple-sync\CHANGELOG.md`: add version `1.2.0`.
- Modify `C:\workspace\simple-sync\VERSION`: set `1.2.0`.
- Modify `C:\workspace\simple-sync\SimpleSync.csproj`: set `<Version>1.2.0</Version>`.
- Modify `C:\workspace\simple-sync\HARNESS.md` and `C:\workspace\simple-sync\HARNESS_kor.md`: add manual localization verification steps.

## Task 1: Localization Core and Config

**Files:**
- Create: `C:\workspace\simple-sync\LocalizationService.cs`
- Modify: `C:\workspace\simple-sync\Models.cs`
- Modify: `C:\workspace\simple-sync\ConfigService.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`
- Modify: `C:\workspace\simple-sync\config.example.toml`

- [ ] **Step 1: Add failing localization and config tests**

Add these `Run` calls after the existing filter summary test registrations in `tests/SimpleSync.Tests/Program.cs`:

```csharp
await Run("localization falls back to Korean for unsupported language", LocalizationFallsBackToKoreanForUnsupportedLanguage);
await Run("localization returns key for missing text", LocalizationReturnsKeyForMissingText);
await Run("localization formats strings", LocalizationFormatsStrings);
await Run("config round-trips language", ConfigRoundTripsLanguage);
await Run("default filter presets can be localized", DefaultFilterPresetsCanBeLocalized);
```

Add these test methods near the existing config and filter tests:

```csharp
static Task LocalizationFallsBackToKoreanForUnsupportedLanguage()
{
    var localization = new LocalizationService("fr-FR");

    Assert(localization.LanguageCode == LocalizationService.DefaultLanguage, "unsupported language should fall back to default");
    Assert(localization.Text("toolbar.sync_now") == "지금 동기화", "fallback language should be Korean");
    Assert(localization.ModeLabel(SyncModes.Copy) == "변경 파일 복사", "copy mode should have Korean label");
    Assert(localization.ModeLabel(SyncModes.Mirror) == "소스 미러링", "mirror mode should have Korean label");

    return Task.CompletedTask;
}

static Task LocalizationReturnsKeyForMissingText()
{
    var localization = new LocalizationService("en-US");

    Assert(localization.Text("missing.key") == "missing.key", "missing localization key should return the key");

    return Task.CompletedTask;
}

static Task LocalizationFormatsStrings()
{
    var localization = new LocalizationService("en-US");

    Assert(
        localization.Format("activity.completed", "Pair 1", 2, 3, 4, 5, 6) == "[Pair 1] Done: copied 2, skipped 3, deleted 4, excluded 5, failed 6",
        "formatted English activity text should include all arguments");

    return Task.CompletedTask;
}

static Task ConfigRoundTripsLanguage()
{
    using var workspace = TestWorkspace.Create();
    var configPath = Path.Combine(workspace.Root, "config.toml");
    var service = new ConfigService(configPath);

    service.Save(new AppConfig { Language = "en-US", IntervalSeconds = 15 });

    var loaded = service.Load();
    Assert(loaded.Language == "en-US", "language should round-trip through config.toml");

    return Task.CompletedTask;
}

static Task DefaultFilterPresetsCanBeLocalized()
{
    var korean = FilterPreset.CreateDefaults(new LocalizationService("ko-KR"));
    var english = FilterPreset.CreateDefaults(new LocalizationService("en-US"));

    Assert(korean[0].Name == "문서", "Korean default preset should use Korean name");
    Assert(english[0].Name == "Documents", "English default preset should use English name");
    Assert(english.Single(item => item.Name == "Exclude temp/build").ExcludePatterns.Contains("node_modules/**"), "localized defaults should preserve preset values");

    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

Expected: build fails because `LocalizationService`, `AppConfig.Language`, and `FilterPreset.CreateDefaults(LocalizationService)` do not exist yet.

- [ ] **Step 3: Implement `LocalizationService.cs`**

Create `LocalizationService.cs` with this structure:

```csharp
using System.Globalization;

namespace SimpleSync;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed class LocalizationService
{
    public const string DefaultLanguage = "ko-KR";

    private static readonly LanguageOption[] LanguageOptions =
    [
        new("ko-KR", "한국어"),
        new("en-US", "English")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ko-KR"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["app.title"] = "simple sync",
                ["app.title_debug"] = "simple sync (Debug)",
                ["app.subtitle"] = "로컬 및 네트워크 폴더 단방향 파일 동기화",
                ["toolbar.every"] = "매",
                ["toolbar.seconds"] = "초",
                ["toolbar.auto"] = "자동",
                ["toolbar.sync_now"] = "지금 동기화",
                ["toolbar.add_pair"] = "쌍 추가",
                ["toolbar.remove"] = "삭제",
                ["toolbar.choose_source"] = "소스 선택",
                ["toolbar.choose_target"] = "타겟 선택",
                ["toolbar.edit_filter"] = "필터 편집",
                ["toolbar.skin"] = "스킨",
                ["toolbar.language"] = "언어",
                ["section.sync_pairs"] = "동기화 쌍",
                ["section.activity"] = "작업 기록",
                ["grid.on"] = "On",
                ["grid.name"] = "이름",
                ["grid.mode"] = "방식",
                ["grid.filter"] = "필터",
                ["grid.progress"] = "진행",
                ["grid.source"] = "소스",
                ["grid.flow"] = "방향",
                ["grid.target"] = "타겟",
                ["mode.copy"] = "변경 파일 복사",
                ["mode.mirror"] = "소스 미러링",
                ["filter.summary.all"] = "All files",
                ["filter.summary.filtered"] = "Filtered",
                ["activity.filter.all"] = "All",
                ["activity.filter.selected"] = "Selected",
                ["activity.filter.errors"] = "Errors",
                ["activity.no_pair_selected"] = "선택된 동기화 쌍 없음",
                ["activity.selected_status"] = "선택: {0} - {1}",
                ["activity.status.idle"] = "대기",
                ["activity.status.scanning"] = "스캔 중",
                ["activity.status.copying"] = "복사 중",
                ["activity.status.deleting"] = "삭제 중",
                ["activity.status.done"] = "완료",
                ["activity.status.failed"] = "실패",
                ["activity.no_targets"] = "대상이 없습니다.",
                ["activity.already_running"] = "이미 동기화가 실행 중입니다.",
                ["activity.manual_reason"] = "수동 실행",
                ["activity.auto_reason"] = "자동 실행",
                ["activity.started"] = "{0} 시작",
                ["activity.cancelled"] = "동기화가 취소되었습니다.",
                ["activity.failed"] = "실패: {0}",
                ["activity.pair.started"] = "[{0}] 시작",
                ["activity.pair.mode"] = "[{0}] 모드: {1}",
                ["activity.pair.path"] = "[{0}] 진행경로: {1} -> {2}",
                ["activity.pair.filter"] = "[{0}] 필터: {1}",
                ["activity.completed"] = "[{0}] 완료: 복사 {1}, 유지 {2}, 삭제 {3}, 제외 {4}, 실패 {5}",
                ["activity.message"] = "[{0}] - {1}",
                ["activity.clipboard_no_path"] = "클립보드에 붙여넣을 텍스트 경로가 없습니다.",
                ["status.ready"] = "대기",
                ["status.auto_every"] = "자동 동기화 {0}초마다 실행",
                ["progress.done"] = "완료",
                ["progress.failed"] = "실패",
                ["dialog.source_description"] = "소스 폴더 선택",
                ["dialog.target_description"] = "타겟 폴더 선택",
                ["filter.title"] = "\"{0}\" 필터",
                ["filter.intro"] = "확장자, 특정 파일, 포함 패턴은 OR 조건입니다. 하나라도 맞으면 포함됩니다.\r\n제외 패턴은 마지막에 적용되며 항상 우선합니다.",
                ["filter.quick_presets"] = "빠른 프리셋: 하나 이상의 버튼을 눌러 추천 확장자 또는 제외 패턴을 추가합니다.",
                ["filter.extensions.label"] = "확장자",
                ["filter.extensions.hint"] = "포함할 파일 형식입니다. 예: .md, .pdf, .jpg",
                ["filter.extensions.tooltip"] = "같은 확장자의 파일을 모두 동기화할 때 사용합니다.",
                ["filter.files.label"] = "특정 파일",
                ["filter.files.hint"] = "소스 폴더 기준의 정확한 파일입니다. 예: README.md, docs/setup.md",
                ["filter.files.tooltip"] = "C:\\source\\docs\\setup.md 파일은 docs/setup.md 로 입력합니다.",
                ["filter.include.label"] = "포함 패턴",
                ["filter.include.hint"] = "포함할 폴더 또는 파일명 규칙입니다. 예: docs/**, reports/*.pdf",
                ["filter.include.tooltip"] = "확장자나 특정 파일만으로 부족할 때 glob 스타일 규칙을 사용합니다.",
                ["filter.exclude.label"] = "제외 패턴",
                ["filter.exclude.hint"] = "포함 규칙 이후 항상 제외합니다. 예: bin/**, obj/**, *.tmp",
                ["filter.exclude.tooltip"] = "제외는 확장자, 특정 파일, 포함 패턴보다 우선합니다.",
                ["filter.include_subdirectories"] = "하위 폴더 포함",
                ["button.apply"] = "적용",
                ["button.cancel"] = "취소",
                ["button.reset"] = "초기화",
                ["preset.documents"] = "문서",
                ["preset.images"] = "이미지",
                ["preset.code"] = "코드",
                ["preset.archives"] = "압축",
                ["preset.exclude_temp_build"] = "임시/빌드 제외"
            },
            ["en-US"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["app.title"] = "simple sync",
                ["app.title_debug"] = "simple sync (Debug)",
                ["app.subtitle"] = "One-way file synchronization for local and network folders",
                ["toolbar.every"] = "Every",
                ["toolbar.seconds"] = "sec",
                ["toolbar.auto"] = "Auto",
                ["toolbar.sync_now"] = "Sync Now",
                ["toolbar.add_pair"] = "Add Pair",
                ["toolbar.remove"] = "Remove",
                ["toolbar.choose_source"] = "Choose Source",
                ["toolbar.choose_target"] = "Choose Target",
                ["toolbar.edit_filter"] = "Edit Filter...",
                ["toolbar.skin"] = "Skin",
                ["toolbar.language"] = "Language",
                ["section.sync_pairs"] = "Sync pairs",
                ["section.activity"] = "Activity",
                ["grid.on"] = "On",
                ["grid.name"] = "Name",
                ["grid.mode"] = "Mode",
                ["grid.filter"] = "Filter",
                ["grid.progress"] = "Progress",
                ["grid.source"] = "Source",
                ["grid.flow"] = "Flow",
                ["grid.target"] = "Target",
                ["mode.copy"] = "Copy changes",
                ["mode.mirror"] = "Mirror source",
                ["filter.summary.all"] = "All files",
                ["filter.summary.filtered"] = "Filtered",
                ["activity.filter.all"] = "All",
                ["activity.filter.selected"] = "Selected",
                ["activity.filter.errors"] = "Errors",
                ["activity.no_pair_selected"] = "No sync pair selected",
                ["activity.selected_status"] = "Selected: {0} - {1}",
                ["activity.status.idle"] = "Idle",
                ["activity.status.scanning"] = "Scanning",
                ["activity.status.copying"] = "Copying",
                ["activity.status.deleting"] = "Deleting",
                ["activity.status.done"] = "Done",
                ["activity.status.failed"] = "Failed",
                ["activity.no_targets"] = "No sync targets.",
                ["activity.already_running"] = "Sync is already running.",
                ["activity.manual_reason"] = "Manual sync",
                ["activity.auto_reason"] = "Auto sync",
                ["activity.started"] = "{0} started",
                ["activity.cancelled"] = "Sync was cancelled.",
                ["activity.failed"] = "Failed: {0}",
                ["activity.pair.started"] = "[{0}] Start",
                ["activity.pair.mode"] = "[{0}] Mode: {1}",
                ["activity.pair.path"] = "[{0}] Path: {1} -> {2}",
                ["activity.pair.filter"] = "[{0}] Filter: {1}",
                ["activity.completed"] = "[{0}] Done: copied {1}, skipped {2}, deleted {3}, excluded {4}, failed {5}",
                ["activity.message"] = "[{0}] - {1}",
                ["activity.clipboard_no_path"] = "Clipboard does not contain a text path.",
                ["status.ready"] = "Ready",
                ["status.auto_every"] = "Auto sync every {0} sec",
                ["progress.done"] = "Done",
                ["progress.failed"] = "Failed",
                ["dialog.source_description"] = "Choose source folder",
                ["dialog.target_description"] = "Choose target folder",
                ["filter.title"] = "Filter for \"{0}\"",
                ["filter.intro"] = "Extensions, Specific files, and Include patterns are combined as OR rules: a file is included if it matches any one of them.\r\nExclude patterns are applied last and always win, even when a file was included above.",
                ["filter.quick_presets"] = "Quick presets: click one or more buttons to append suggested extensions or exclude patterns.",
                ["filter.extensions.label"] = "Extensions",
                ["filter.extensions.hint"] = "File types to include. Example: .md, .pdf, .jpg",
                ["filter.extensions.tooltip"] = "Use this when every file with the same extension should sync.",
                ["filter.files.label"] = "Specific files",
                ["filter.files.hint"] = "Exact files from the source folder. Example: README.md, docs/setup.md",
                ["filter.files.tooltip"] = "Use source-relative paths. For C:\\source\\docs\\setup.md, enter docs/setup.md.",
                ["filter.include.label"] = "Include patterns",
                ["filter.include.hint"] = "Folder or filename rules to include. Example: docs/**, reports/*.pdf",
                ["filter.include.tooltip"] = "Use glob-style rules when extensions or exact files are not enough.",
                ["filter.exclude.label"] = "Exclude patterns",
                ["filter.exclude.hint"] = "Always excluded after include rules. Example: bin/**, obj/**, *.tmp",
                ["filter.exclude.tooltip"] = "Exclude wins over Extensions, Specific files, and Include patterns.",
                ["filter.include_subdirectories"] = "Include subdirectories",
                ["button.apply"] = "Apply",
                ["button.cancel"] = "Cancel",
                ["button.reset"] = "Reset",
                ["preset.documents"] = "Documents",
                ["preset.images"] = "Images",
                ["preset.code"] = "Code",
                ["preset.archives"] = "Archives",
                ["preset.exclude_temp_build"] = "Exclude temp/build"
            }
        };

    public LocalizationService(string? languageCode = null)
    {
        LanguageCode = NormalizeLanguage(languageCode);
    }

    public string LanguageCode { get; private set; }

    public static IReadOnlyList<LanguageOption> SupportedLanguages => LanguageOptions;

    public static string NormalizeLanguage(string? languageCode)
    {
        return LanguageOptions.Any(item => item.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            ? LanguageOptions.First(item => item.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase)).Code
            : DefaultLanguage;
    }

    public void SetLanguage(string? languageCode)
    {
        LanguageCode = NormalizeLanguage(languageCode);
    }

    public string Text(string key)
    {
        if (Strings.TryGetValue(LanguageCode, out var current) && current.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Strings[DefaultLanguage].TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Text(key), args);
    }

    public string ModeLabel(string? mode)
    {
        return Text(SyncModes.Normalize(mode) == SyncModes.Mirror ? "mode.mirror" : "mode.copy");
    }

    public string FilterSummary(bool hasRules)
    {
        return Text(hasRules ? "filter.summary.filtered" : "filter.summary.all");
    }

    public FilterPreset LocalizeDefaultPreset(FilterPreset preset)
    {
        var name = preset.Name switch
        {
            "Documents" => Text("preset.documents"),
            "Images" => Text("preset.images"),
            "Code" => Text("preset.code"),
            "Archives" => Text("preset.archives"),
            "Exclude temp/build" => Text("preset.exclude_temp_build"),
            _ => preset.Name
        };

        return new FilterPreset
        {
            Name = name,
            Extensions = [.. preset.Extensions],
            Files = [.. preset.Files],
            IncludePatterns = [.. preset.IncludePatterns],
            ExcludePatterns = [.. preset.ExcludePatterns]
        };
    }
}
```

- [ ] **Step 4: Add language to models and config**

In `Models.cs`, change `AppConfig` to include:

```csharp
public string Language { get; set; } = LocalizationService.DefaultLanguage;
```

In `FilterPreset`, keep the existing `CreateDefaults()` method and add an overload:

```csharp
public static List<FilterPreset> CreateDefaults(LocalizationService localization)
{
    return CreateDefaults().Select(localization.LocalizeDefaultPreset).ToList();
}
```

In `ConfigService.Load()`, add this top-level parse branch after `skin`:

```csharp
else if (key.Equals("language", StringComparison.OrdinalIgnoreCase))
{
    config.Language = LocalizationService.NormalizeLanguage(Unquote(value));
}
```

In `ConfigService.Save()`, write language after skin:

```csharp
builder.AppendLine($"language = \"{Escape(LocalizationService.NormalizeLanguage(config.Language))}\"");
```

In `config.example.toml`, add:

```toml
language = "ko-KR"
```

- [ ] **Step 5: Run tests and commit Task 1**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

Expected: all tests pass.

Commit:

```powershell
git -C C:\workspace\simple-sync add LocalizationService.cs Models.cs ConfigService.cs tests/SimpleSync.Tests/Program.cs config.example.toml
git -C C:\workspace\simple-sync commit -m "Add localization core"
```

## Task 2: Localize Main Window

**Files:**
- Modify: `C:\workspace\simple-sync\MainForm.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`

- [ ] **Step 1: Add failing tests for localized MainForm helpers**

Add these `Run` calls near the existing debug/status tests:

```csharp
await Run("window title localizes debug state", WindowTitleLocalizesDebugState);
await Run("selected pair current status localizes", SelectedPairCurrentStatusLocalizes);
await Run("activity filter visibility uses stable keys", ActivityFilterVisibilityUsesStableKeys);
```

Add these test methods:

```csharp
static Task WindowTitleLocalizesDebugState()
{
    var korean = new LocalizationService("ko-KR");
    var english = new LocalizationService("en-US");

    Assert(SimpleSync.MainForm.FormatWindowTitle(false, korean) == "simple sync", "Korean release title should match app title");
    Assert(SimpleSync.MainForm.FormatWindowTitle(true, english) == "simple sync (Debug)", "English debug title should include Debug");

    return Task.CompletedTask;
}

static Task SelectedPairCurrentStatusLocalizes()
{
    var pair = new SyncPair { Name = "Docs", Source = @"C:\source", Target = @"D:\target" };
    var korean = new LocalizationService("ko-KR");
    var english = new LocalizationService("en-US");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, null, korean) == "선택: Docs - 대기",
        "Korean missing progress should show localized idle status");

    Assert(
        SimpleSync.MainForm.FormatCurrentStatus(pair, new SyncProgress { Pair = pair, Phase = SyncPhase.Preparing }, english) == "Selected: Docs - Scanning",
        "English preparing status should show localized scanning status");

    return Task.CompletedTask;
}

static Task ActivityFilterVisibilityUsesStableKeys()
{
    Assert(SimpleSync.MainForm.NormalizeActivityFilter("activity.filter.selected") == "activity.filter.selected", "stable selected key should remain unchanged");
    Assert(SimpleSync.MainForm.NormalizeActivityFilter("Selected") == "activity.filter.selected", "legacy selected display text should map to stable key");
    Assert(SimpleSync.MainForm.NormalizeActivityFilter("Errors") == "activity.filter.errors", "legacy errors display text should map to stable key");
    Assert(SimpleSync.MainForm.NormalizeActivityFilter("anything") == "activity.filter.all", "unknown activity filter should default to all");

    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

Expected: build fails because the overloaded helpers and `NormalizeActivityFilter` do not exist yet.

- [ ] **Step 3: Add language fields and selector**

In `MainForm.cs`, replace the static mode option array with instance rebuilding. Add fields:

```csharp
private readonly LocalizationService _localization = new();
private readonly ComboBox _languageSelect = new();
private readonly Label _headerTitleLabel = new();
private readonly Label _headerSubtitleLabel = new();
private readonly Label _syncPairsTitleLabel = new();
private readonly Label _activityTitleLabel = new();
private readonly Label _everyLabel = new();
private readonly Label _secondsLabel = new();
private readonly Label _skinLabel = new();
private readonly Label _languageLabel = new();
private readonly List<ModeOption> _modeOptions = [];
```

Change toolbar/header creation so labels that must change language are stored in these fields instead of being local variables.

Configure `_languageSelect` beside `_skinSelect`:

```csharp
_languageSelect.DropDownStyle = ComboBoxStyle.DropDownList;
_languageSelect.Width = 120;
_comboBoxes.Add(_languageSelect);
foreach (var language in LocalizationService.SupportedLanguages)
{
    _languageSelect.Items.Add(language);
}
_languageSelect.DisplayMember = nameof(LanguageOption.DisplayName);
_languageSelect.ValueMember = nameof(LanguageOption.Code);
_languageSelect.SelectedIndexChanged += (_, _) =>
{
    if (!_isLoadingConfig && _languageSelect.SelectedItem is LanguageOption language)
    {
        _localization.SetLanguage(language.Code);
        ApplyLanguage();
        SaveConfig();
    }
};
```

- [ ] **Step 4: Implement localized helper methods**

Add or update these public static helper methods in `MainForm.cs`:

```csharp
public static string FormatWindowTitle(bool isDebugBuild, LocalizationService localization)
{
    return localization.Text(isDebugBuild ? "app.title_debug" : "app.title");
}

public static string FormatWindowTitle(bool isDebugBuild)
{
    return FormatWindowTitle(isDebugBuild, new LocalizationService());
}

public static string FormatStatus(string version, string message, bool isDebugBuild)
{
    var debugPart = isDebugBuild ? " Debug" : string.Empty;
    return $"simple sync {version}{debugPart} | {message}";
}

public static string FormatCurrentStatus(SyncPair? pair, SyncProgress? progress, LocalizationService localization)
{
    if (pair is null)
    {
        return localization.Text("activity.no_pair_selected");
    }

    var pairName = PairLogName(pair);
    var status = progress?.Phase switch
    {
        SyncPhase.Preparing => localization.Text("activity.status.scanning"),
        SyncPhase.Copying => localization.Text("activity.status.copying"),
        SyncPhase.Deleting => localization.Text("activity.status.deleting"),
        SyncPhase.Completed => localization.Text("activity.status.done"),
        SyncPhase.Failed => localization.Text("activity.status.failed"),
        _ => localization.Text("activity.status.idle")
    };

    return localization.Format("activity.selected_status", pairName, status);
}

public static string FormatCurrentStatus(SyncPair? pair, SyncProgress? progress)
{
    return FormatCurrentStatus(pair, progress, new LocalizationService("en-US"));
}

public static string NormalizeActivityFilter(string? filter)
{
    return filter switch
    {
        "activity.filter.selected" or "Selected" => "activity.filter.selected",
        "activity.filter.errors" or "Errors" => "activity.filter.errors",
        _ => "activity.filter.all"
    };
}
```

Keep the existing English overload behavior because existing tests assert English helper outputs.

- [ ] **Step 5: Implement `ApplyLanguage()` and localized UI updates**

Add `ApplyLanguage()` in `MainForm.cs`:

```csharp
private void ApplyLanguage()
{
    Text = FormatWindowTitle(_isDebugBuild, _localization);
    _headerTitleLabel.Text = _localization.Text("app.title");
    _headerSubtitleLabel.Text = _localization.Text("app.subtitle");
    _everyLabel.Text = _localization.Text("toolbar.every");
    _secondsLabel.Text = _localization.Text("toolbar.seconds");
    _autoSyncCheck.Text = _localization.Text("toolbar.auto");
    _nowButton.Text = _localization.Text("toolbar.sync_now");
    _addButton.Text = _localization.Text("toolbar.add_pair");
    _removeButton.Text = _localization.Text("toolbar.remove");
    _browseSourceButton.Text = _localization.Text("toolbar.choose_source");
    _browseTargetButton.Text = _localization.Text("toolbar.choose_target");
    _filterButton.Text = _localization.Text("toolbar.edit_filter");
    _skinLabel.Text = _localization.Text("toolbar.skin");
    _languageLabel.Text = _localization.Text("toolbar.language");
    _syncPairsTitleLabel.Text = _localization.Text("section.sync_pairs");
    _activityTitleLabel.Text = _localization.Text("section.activity");

    SetColumnHeader(nameof(SyncPair.Enabled), _localization.Text("grid.on"));
    SetColumnHeader(nameof(SyncPair.Name), _localization.Text("grid.name"));
    SetColumnHeader(nameof(SyncPair.Mode), _localization.Text("grid.mode"));
    SetColumnHeader("Filter", _localization.Text("grid.filter"));
    SetColumnHeader("Progress", _localization.Text("grid.progress"));
    SetColumnHeader(nameof(SyncPair.Source), _localization.Text("grid.source"));
    SetColumnHeader("Flow", _localization.Text("grid.flow"));
    SetColumnHeader(nameof(SyncPair.Target), _localization.Text("grid.target"));

    RebuildModeOptions();
    RebuildActivityFilterOptions();
    SelectCurrentLanguage();
    UpdateCurrentStatusLabel();
    UpdateStatus();
    _grid.Refresh();
}
```

Add helpers:

```csharp
private void SetColumnHeader(string nameOrProperty, string text)
{
    foreach (DataGridViewColumn column in _grid.Columns)
    {
        if (column.Name == nameOrProperty || column.DataPropertyName == nameOrProperty)
        {
            column.HeaderText = text;
            return;
        }
    }
}

private void RebuildModeOptions()
{
    _modeOptions.Clear();
    _modeOptions.Add(new ModeOption(SyncModes.Copy, _localization.ModeLabel(SyncModes.Copy)));
    _modeOptions.Add(new ModeOption(SyncModes.Mirror, _localization.ModeLabel(SyncModes.Mirror)));

    if (_grid.Columns[nameof(SyncPair.Mode)] is DataGridViewComboBoxColumn modeColumn)
    {
        modeColumn.DataSource = null;
        modeColumn.DataSource = _modeOptions;
        modeColumn.ValueMember = nameof(ModeOption.Value);
        modeColumn.DisplayMember = nameof(ModeOption.Label);
    }
}

private void RebuildActivityFilterOptions()
{
    var selectedKey = GetActivityFilter();
    _activityFilterSelect.Items.Clear();
    _activityFilterSelect.Items.Add(new ActivityFilterOption("activity.filter.all", _localization.Text("activity.filter.all")));
    _activityFilterSelect.Items.Add(new ActivityFilterOption("activity.filter.selected", _localization.Text("activity.filter.selected")));
    _activityFilterSelect.Items.Add(new ActivityFilterOption("activity.filter.errors", _localization.Text("activity.filter.errors")));
    _activityFilterSelect.DisplayMember = nameof(ActivityFilterOption.Label);
    _activityFilterSelect.ValueMember = nameof(ActivityFilterOption.Key);
    SelectActivityFilter(selectedKey);
}
```

Add a private record:

```csharp
private sealed record ActivityFilterOption(string Key, string Label);
```

- [ ] **Step 6: Wire language into config, logs, status, and filters**

In `LoadConfig()`, after loading config, call:

```csharp
_localization.SetLanguage(config.Language);
```

Then localize default presets when no custom presets were loaded by using:

```csharp
_filterPresets = config.FilterPresets.Count == 0
    ? FilterPreset.CreateDefaults(_localization)
    : config.FilterPresets;
```

In `SaveConfig()`, add:

```csharp
Language = _localization.LanguageCode,
```

In `RunSyncAsync`, replace hard-coded log text with localization calls:

```csharp
AppendLog(_localization.Format("activity.started", reason));
AppendLog(_localization.Text("activity.no_targets"));
AppendLog(_localization.Format("activity.pair.started", pairName), pair);
AppendLog(_localization.Format("activity.pair.mode", pairName, _localization.ModeLabel(pair.Mode)), pair);
AppendLog(_localization.Format("activity.pair.path", pairName, LogPath(pair.Source), LogPath(pair.Target)), pair);
AppendLog(_localization.Format("activity.pair.filter", pairName, pairFilter.Summary), pair);
AppendLog(_localization.Format("activity.completed", pairName, result.CopiedFiles, result.SkippedFiles, result.DeletedFiles, result.ExcludedFiles, result.FailedFiles), pair, result.FailedFiles > 0);
AppendLog(_localization.Format("activity.message", pairName, message), pair, IsErrorMessage(message));
AppendLog(_localization.Text("activity.cancelled"));
AppendLog(_localization.Format("activity.failed", ex.Message), isError: true);
```

Use localized run reasons in button and timer handlers:

```csharp
await RunSyncAsync(_localization.Text("activity.manual_reason"));
await RunSyncAsync(_localization.Text("activity.auto_reason"));
```

In grid filter formatting, display:

```csharp
var filter = SyncFilter.FromPair(filterPair);
e.Value = _localization.FilterSummary(filter.HasRules);
```

Keep tooltip detail unchanged.

- [ ] **Step 7: Run tests and commit Task 2**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

Expected: all tests pass.

Commit:

```powershell
git -C C:\workspace\simple-sync add MainForm.cs tests/SimpleSync.Tests/Program.cs
git -C C:\workspace\simple-sync commit -m "Localize main window"
```

## Task 3: Localize Filter Dialog

**Files:**
- Modify: `C:\workspace\simple-sync\FilterDialog.cs`
- Modify: `C:\workspace\simple-sync\MainForm.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`

- [ ] **Step 1: Add failing tests for localized filter dialog defaults**

Add this `Run` call near filter tests:

```csharp
await Run("filter dialog title text is localizable", FilterDialogTitleTextIsLocalizable);
```

Add this test:

```csharp
static Task FilterDialogTitleTextIsLocalizable()
{
    var pair = new SyncPair { Name = "Docs" };
    var korean = new LocalizationService("ko-KR");
    var english = new LocalizationService("en-US");

    Assert(FilterDialog.FormatTitle(pair, korean) == "\"Docs\" 필터", "Korean dialog title should be localized");
    Assert(FilterDialog.FormatTitle(pair, english) == "Filter for \"Docs\"", "English dialog title should be localized");

    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

Expected: build fails because `FilterDialog.FormatTitle` does not exist yet.

- [ ] **Step 3: Add localization constructor and title helper**

In `FilterDialog.cs`, add field:

```csharp
private readonly LocalizationService _localization;
```

Change constructors:

```csharp
public FilterDialog(SyncPair pair)
    : this(pair, FilterPreset.CreateDefaults(), new LocalizationService())
{
}

public FilterDialog(SyncPair pair, IEnumerable<FilterPreset> presets)
    : this(pair, presets, new LocalizationService())
{
}

public FilterDialog(SyncPair pair, IEnumerable<FilterPreset> presets, LocalizationService localization)
{
    _localization = localization;
    Text = FormatTitle(pair, _localization);
    ...
}
```

Add:

```csharp
public static string FormatTitle(SyncPair pair, LocalizationService localization)
{
    var pairName = string.IsNullOrWhiteSpace(pair.Name) ? "Pair" : pair.Name.Trim();
    return localization.Format("filter.title", pairName);
}
```

- [ ] **Step 4: Replace hard-coded filter dialog text**

In `BuildLayout()`, replace dialog strings with localization keys:

```csharp
Text = FormatTitle(pair, _localization);
intro.Text = _localization.Text("filter.intro");
quickPresetLabel.Text = _localization.Text("filter.quick_presets");
CreateInputGroup(_localization.Text("filter.extensions.label"), _localization.Text("filter.extensions.hint"), _localization.Text("filter.extensions.tooltip"), _extensionsInput);
CreateInputGroup(_localization.Text("filter.files.label"), _localization.Text("filter.files.hint"), _localization.Text("filter.files.tooltip"), _filesInput);
CreateInputGroup(_localization.Text("filter.include.label"), _localization.Text("filter.include.hint"), _localization.Text("filter.include.tooltip"), _includeInput);
CreateInputGroup(_localization.Text("filter.exclude.label"), _localization.Text("filter.exclude.hint"), _localization.Text("filter.exclude.tooltip"), _excludeInput);
_includeSubdirectoriesCheck.Text = _localization.Text("filter.include_subdirectories");
var applyButton = new Button { Text = _localization.Text("button.apply"), DialogResult = DialogResult.OK, AutoSize = true };
var cancelButton = new Button { Text = _localization.Text("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
var resetButton = new Button { Text = _localization.Text("button.reset"), AutoSize = true };
```

Keep user-configured preset names as provided.

- [ ] **Step 5: Pass localization from MainForm**

In `MainForm.OpenFilterDialog()`, change:

```csharp
using var dialog = new FilterDialog(pair, _filterPresets, _localization);
```

In `BrowseSelectedPath`, set folder browser descriptions:

```csharp
Description = isSource ? _localization.Text("dialog.source_description") : _localization.Text("dialog.target_description")
```

- [ ] **Step 6: Run tests and commit Task 3**

Run:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

Expected: all tests pass.

Commit:

```powershell
git -C C:\workspace\simple-sync add FilterDialog.cs MainForm.cs tests/SimpleSync.Tests/Program.cs
git -C C:\workspace\simple-sync commit -m "Localize filter dialog"
```

## Task 4: Version, Docs, and Final Verification

**Files:**
- Modify: `C:\workspace\simple-sync\SimpleSync.csproj`
- Modify: `C:\workspace\simple-sync\VERSION`
- Modify: `C:\workspace\simple-sync\CHANGELOG.md`
- Modify: `C:\workspace\simple-sync\README.md`
- Modify: `C:\workspace\simple-sync\HARNESS.md`
- Modify: `C:\workspace\simple-sync\HARNESS_kor.md`

- [ ] **Step 1: Bump version to 1.2.0**

In `SimpleSync.csproj`, set:

```xml
<Version>1.2.0</Version>
```

In `VERSION`, set:

```text
1.2.0
```

- [ ] **Step 2: Update changelog**

Add this section at the top of `CHANGELOG.md`:

```markdown
## 1.2.0 - 2026-05-26

- Added Korean/English language selection in the main toolbar.
- Added `language` persistence in `config.toml`.
- Localized main window labels, sync mode display names, activity status text, and filter dialog labels.
- Kept internal sync mode values and existing config compatibility unchanged.
```

- [ ] **Step 3: Update Korean README**

In `README.md`, update the version reference to `1.2.0` and add a Korean section:

```markdown
## 다국어 설정

- 상단 툴바의 `언어` 콤보박스에서 `한국어` 또는 `English`를 선택할 수 있습니다.
- 선택한 언어는 `config.toml`의 `language` 값으로 저장되며 다음 실행 시 자동으로 복원됩니다.
- 기존 설정 파일에 `language`가 없어도 실행 가능하며 기본값은 `ko-KR`입니다.
- 내부 동기화 방식 값은 계속 `copy`, `mirror`로 저장되므로 기존 설정과 호환됩니다.
```

- [ ] **Step 4: Update harness docs**

In `HARNESS.md`, add:

```markdown
## Localization Manual Check

1. Start the app with no `language` key in `config.toml`; confirm Korean is selected.
2. Switch Language to English; confirm toolbar, grid headers, status text, activity filter, and filter dialog labels update immediately.
3. Close and reopen the app; confirm English is restored from `config.toml`.
4. Switch back to Korean; confirm `language = "ko-KR"` is saved.
5. Run a sync in both languages and confirm file copy behavior is unchanged.
```

In `HARNESS_kor.md`, add:

```markdown
## 다국어 수동 확인

1. `config.toml`에 `language` 키가 없는 상태로 앱을 실행하고 한국어가 선택되는지 확인합니다.
2. 언어를 English로 변경하고 툴바, 그리드 헤더, 상태 텍스트, Activity 필터, 필터 팝업 문구가 즉시 변경되는지 확인합니다.
3. 앱을 닫았다가 다시 실행하고 English가 복원되는지 확인합니다.
4. 다시 한국어로 변경하고 `language = "ko-KR"`이 저장되는지 확인합니다.
5. 두 언어에서 동기화를 실행해 파일 복사 동작이 변경되지 않았는지 확인합니다.
```

- [ ] **Step 5: Run full verification**

Run:

```powershell
dotnet build C:\workspace\simple-sync\SimpleSync.csproj -c Release
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

Expected: all builds and tests pass.

- [ ] **Step 6: Commit docs and version**

Commit:

```powershell
git -C C:\workspace\simple-sync add SimpleSync.csproj VERSION CHANGELOG.md README.md HARNESS.md HARNESS_kor.md
git -C C:\workspace\simple-sync commit -m "Document localization release"
```

## Final Checks

- [ ] Run `git -C C:\workspace\simple-sync status --short --branch` and confirm the branch is clean.
- [ ] Run `git -C C:\workspace\simple-sync log --oneline -5` and confirm the localization commits are present.
- [ ] If the user asks to publish the branch, push with `git -C C:\workspace\simple-sync push -u origin codex/localization`.
