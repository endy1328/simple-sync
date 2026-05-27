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
                ["status.auto_paused"] = "자동 동기화 일시 중지",
                ["status.syncing"] = "동기화 중...",
                ["progress.pending"] = "대기",
                ["progress.preparing"] = "준비 중",
                ["progress.done"] = "완료",
                ["progress.failed"] = "실패",
                ["progress.deleting"] = "삭제 중",
                ["progress.working"] = "작업 중",
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
                ["status.auto_paused"] = "Auto sync paused",
                ["status.syncing"] = "Syncing...",
                ["progress.pending"] = "Pending",
                ["progress.preparing"] = "Preparing",
                ["progress.done"] = "Done",
                ["progress.failed"] = "Failed",
                ["progress.deleting"] = "Deleting",
                ["progress.working"] = "Working",
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
