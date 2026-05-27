# simple sync 다국어 기능 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `simple sync`에 한국어/영어 UI 다국어 기능을 추가하고, 선택한 언어를 `config.toml`에 저장하며, 기존 동기화 동작은 그대로 유지한다.

**Architecture:** `ko-KR`, `en-US` 내부 사전을 가진 작은 key 기반 localization 계층을 추가한다. WinForms UI는 현재처럼 코드에서 직접 컨트롤을 구성하되, `MainForm`과 `FilterDialog`가 기존 컨트롤의 텍스트만 즉시 갱신한다.

**Tech Stack:** .NET 10, C# 14, Windows Forms, 기존 콘솔형 테스트 하네스 `tests/SimpleSync.Tests`.

---

## 파일 구조

- 생성 `C:\workspace\simple-sync\LocalizationService.cs`: 언어 옵션, 번역 사전, 텍스트 조회, 포맷팅, 모드 라벨, 필터 프리셋 현지화 헬퍼.
- 수정 `C:\workspace\simple-sync\Models.cs`: `AppConfig.Language` 추가, 기본값은 한국어.
- 수정 `C:\workspace\simple-sync\ConfigService.cs`: 최상위 `language` 로드/저장.
- 수정 `C:\workspace\simple-sync\MainForm.cs`: 언어 선택 콤보박스 추가, 화면 라벨 현지화, 선택 언어 저장, 새 로그/상태 메시지 현지화, 필터 팝업으로 localization 전달.
- 수정 `C:\workspace\simple-sync\FilterDialog.cs`: localization service를 받아 팝업 제목, 안내문, 힌트, 버튼, 기본 프리셋 UI를 현지화.
- 수정 `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`: fallback, config language round-trip, 라벨, 상태 포맷, 필터 프리셋 현지화 테스트 추가.
- 수정 `C:\workspace\simple-sync\config.example.toml`: `language = "ko-KR"` 추가.
- 수정 `C:\workspace\simple-sync\README.md`: 한국어 문서로 언어 선택과 버전 내용 갱신.
- 수정 `C:\workspace\simple-sync\CHANGELOG.md`: `1.2.0` 변경 이력 추가.
- 수정 `C:\workspace\simple-sync\VERSION`: `1.2.0`으로 변경.
- 수정 `C:\workspace\simple-sync\SimpleSync.csproj`: `<Version>1.2.0</Version>`으로 변경.
- 수정 `C:\workspace\simple-sync\HARNESS.md`, `C:\workspace\simple-sync\HARNESS_kor.md`: 다국어 수동 검증 절차 추가.

## Task 1: Localization Core and Config

**Files:**
- Create: `C:\workspace\simple-sync\LocalizationService.cs`
- Modify: `C:\workspace\simple-sync\Models.cs`
- Modify: `C:\workspace\simple-sync\ConfigService.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`
- Modify: `C:\workspace\simple-sync\config.example.toml`

- [ ] **Step 1: 실패하는 localization/config 테스트 추가**

`tests/SimpleSync.Tests/Program.cs`의 기존 filter summary 테스트 등록 뒤에 아래 `Run` 호출을 추가한다.

```csharp
await Run("localization falls back to Korean for unsupported language", LocalizationFallsBackToKoreanForUnsupportedLanguage);
await Run("localization returns key for missing text", LocalizationReturnsKeyForMissingText);
await Run("localization formats strings", LocalizationFormatsStrings);
await Run("config round-trips language", ConfigRoundTripsLanguage);
await Run("default filter presets can be localized", DefaultFilterPresetsCanBeLocalized);
```

기존 config/filter 테스트 근처에 아래 테스트 메서드를 추가한다.

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

- [ ] **Step 2: 테스트를 실행해서 실패 확인**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

예상: `LocalizationService`, `AppConfig.Language`, `FilterPreset.CreateDefaults(LocalizationService)`가 없어서 빌드 실패.

- [ ] **Step 3: `LocalizationService.cs` 구현**

영문 구현 계획서의 Task 1 Step 3에 있는 전체 `LocalizationService.cs` 코드를 그대로 생성한다. 주요 요구사항은 아래와 같다.

```csharp
public sealed record LanguageOption(string Code, string DisplayName);

public sealed class LocalizationService
{
    public const string DefaultLanguage = "ko-KR";
    public string LanguageCode { get; private set; }
    public static IReadOnlyList<LanguageOption> SupportedLanguages => LanguageOptions;
    public static string NormalizeLanguage(string? languageCode) { ... }
    public void SetLanguage(string? languageCode) { ... }
    public string Text(string key) { ... }
    public string Format(string key, params object[] args) { ... }
    public string ModeLabel(string? mode) { ... }
    public string FilterSummary(bool hasRules) { ... }
    public FilterPreset LocalizeDefaultPreset(FilterPreset preset) { ... }
}
```

사전에는 최소한 영문 구현 계획서에 명시된 모든 key를 포함한다.

- [ ] **Step 4: model/config에 language 추가**

`Models.cs`의 `AppConfig`에 추가:

```csharp
public string Language { get; set; } = LocalizationService.DefaultLanguage;
```

`FilterPreset`에 overload 추가:

```csharp
public static List<FilterPreset> CreateDefaults(LocalizationService localization)
{
    return CreateDefaults().Select(localization.LocalizeDefaultPreset).ToList();
}
```

`ConfigService.Load()`의 최상위 설정 파싱에 추가:

```csharp
else if (key.Equals("language", StringComparison.OrdinalIgnoreCase))
{
    config.Language = LocalizationService.NormalizeLanguage(Unquote(value));
}
```

`ConfigService.Save()`에서 `skin` 다음에 저장:

```csharp
builder.AppendLine($"language = \"{Escape(LocalizationService.NormalizeLanguage(config.Language))}\"");
```

`config.example.toml`에 추가:

```toml
language = "ko-KR"
```

- [ ] **Step 5: 테스트 후 Task 1 커밋**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

예상: 모든 테스트 통과.

커밋:

```powershell
git -C C:\workspace\simple-sync add LocalizationService.cs Models.cs ConfigService.cs tests/SimpleSync.Tests/Program.cs config.example.toml
git -C C:\workspace\simple-sync commit -m "Add localization core"
```

## Task 2: 메인 화면 현지화

**Files:**
- Modify: `C:\workspace\simple-sync\MainForm.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`

- [ ] **Step 1: MainForm helper 현지화 테스트 추가**

영문 구현 계획서 Task 2 Step 1의 테스트 3개를 추가한다.

- [ ] **Step 2: 테스트 실행 후 실패 확인**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

예상: overload helper와 `NormalizeActivityFilter`가 없어서 빌드 실패.

- [ ] **Step 3: 언어 필드와 선택 콤보박스 추가**

`MainForm.cs`에 `LocalizationService`, `_languageSelect`, 현지화 대상 label field, `_modeOptions`를 추가한다. 상단 툴바의 Skin 옆에 Language 콤보박스를 추가하고, 선택 변경 시 `_localization.SetLanguage(...)`, `ApplyLanguage()`, `SaveConfig()`를 호출한다.

- [ ] **Step 4: localized helper 구현**

영문 구현 계획서 Task 2 Step 4의 `FormatWindowTitle`, `FormatCurrentStatus`, `NormalizeActivityFilter`를 구현한다. 기존 테스트 호환을 위해 localization 파라미터 없는 overload는 유지한다.

- [ ] **Step 5: `ApplyLanguage()` 구현**

영문 구현 계획서 Task 2 Step 5의 `ApplyLanguage()`, `SetColumnHeader`, `RebuildModeOptions`, `RebuildActivityFilterOptions`를 구현한다.

- [ ] **Step 6: config/log/status/filter에 언어 적용**

`LoadConfig()`에서 config 언어를 적용하고, `SaveConfig()`에 `Language = _localization.LanguageCode`를 포함한다. `RunSyncAsync`의 새 로그 메시지는 localization key로 생성한다. grid filter 표시값은 `All files` 또는 `Filtered`만 표시하되, 현재 언어 사전의 값을 사용한다.

- [ ] **Step 7: 테스트 후 Task 2 커밋**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

예상: 모든 테스트 통과.

커밋:

```powershell
git -C C:\workspace\simple-sync add MainForm.cs tests/SimpleSync.Tests/Program.cs
git -C C:\workspace\simple-sync commit -m "Localize main window"
```

## Task 3: 필터 팝업 현지화

**Files:**
- Modify: `C:\workspace\simple-sync\FilterDialog.cs`
- Modify: `C:\workspace\simple-sync\MainForm.cs`
- Modify: `C:\workspace\simple-sync\tests\SimpleSync.Tests\Program.cs`

- [ ] **Step 1: 필터 팝업 제목 현지화 테스트 추가**

영문 구현 계획서 Task 3 Step 1의 `FilterDialogTitleTextIsLocalizable` 테스트를 추가한다.

- [ ] **Step 2: 테스트 실행 후 실패 확인**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
```

예상: `FilterDialog.FormatTitle`이 없어서 빌드 실패.

- [ ] **Step 3: localization 생성자와 제목 helper 추가**

`FilterDialog`에 `_localization` field를 추가하고, `FilterDialog(SyncPair, IEnumerable<FilterPreset>, LocalizationService)` 생성자를 추가한다. 기존 생성자는 새 생성자로 위임한다. `FormatTitle(SyncPair, LocalizationService)` public static helper를 추가한다.

- [ ] **Step 4: 필터 팝업 하드코딩 문구 교체**

팝업 소개, quick preset 안내, input group label/hint/tooltip, `Include subdirectories`, `Apply`, `Cancel`, `Reset`을 localization key로 교체한다. 사용자가 config에서 바꾼 preset name은 그대로 표시한다.

- [ ] **Step 5: MainForm에서 localization 전달**

필터 팝업을 열 때 아래처럼 호출한다.

```csharp
using var dialog = new FilterDialog(pair, _filterPresets, _localization);
```

폴더 선택 설명도 `dialog.source_description`, `dialog.target_description` key로 교체한다.

- [ ] **Step 6: 테스트 후 Task 3 커밋**

실행:

```powershell
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

예상: 모든 테스트 통과.

커밋:

```powershell
git -C C:\workspace\simple-sync add FilterDialog.cs MainForm.cs tests/SimpleSync.Tests/Program.cs
git -C C:\workspace\simple-sync commit -m "Localize filter dialog"
```

## Task 4: 버전, 문서, 최종 검증

**Files:**
- Modify: `C:\workspace\simple-sync\SimpleSync.csproj`
- Modify: `C:\workspace\simple-sync\VERSION`
- Modify: `C:\workspace\simple-sync\CHANGELOG.md`
- Modify: `C:\workspace\simple-sync\README.md`
- Modify: `C:\workspace\simple-sync\HARNESS.md`
- Modify: `C:\workspace\simple-sync\HARNESS_kor.md`

- [ ] **Step 1: 버전 1.2.0 반영**

`SimpleSync.csproj`:

```xml
<Version>1.2.0</Version>
```

`VERSION`:

```text
1.2.0
```

- [ ] **Step 2: CHANGELOG 갱신**

영문 구현 계획서 Task 4 Step 2의 `1.2.0 - 2026-05-26` 섹션을 추가한다.

- [ ] **Step 3: README 갱신**

`README.md`의 버전 참조를 `1.2.0`으로 변경하고, 한국어로 `다국어 설정` 섹션을 추가한다.

- [ ] **Step 4: HARNESS 문서 갱신**

영문판 `HARNESS.md`에는 영문 수동 검증 절차를 추가하고, `HARNESS_kor.md`에는 같은 내용을 한국어로 추가한다.

- [ ] **Step 5: 전체 검증**

실행:

```powershell
dotnet build C:\workspace\simple-sync\SimpleSync.csproj -c Release
dotnet build C:\workspace\simple-sync\tests\SimpleSync.Tests\SimpleSync.Tests.csproj -c Release
dotnet C:\workspace\simple-sync\tests\SimpleSync.Tests\bin\Release\net10.0-windows\SimpleSync.Tests.dll
```

예상: 모든 빌드와 테스트 통과.

- [ ] **Step 6: 문서/버전 커밋**

커밋:

```powershell
git -C C:\workspace\simple-sync add SimpleSync.csproj VERSION CHANGELOG.md README.md HARNESS.md HARNESS_kor.md
git -C C:\workspace\simple-sync commit -m "Document localization release"
```

## 최종 확인

- [ ] `git -C C:\workspace\simple-sync status --short --branch` 실행 후 브랜치가 clean인지 확인한다.
- [ ] `git -C C:\workspace\simple-sync log --oneline -5` 실행 후 localization 커밋들이 있는지 확인한다.
- [ ] 사용자가 브랜치 게시를 요청하면 `git -C C:\workspace\simple-sync push -u origin codex/localization`을 실행한다.
