# simple sync 다국어 설계

## 목표

`simple sync`에 1차 다국어 시스템을 추가해서, 동기화 동작이나 기존 설정 호환성을 바꾸지 않고 UI를 한국어와 영어로 표시할 수 있게 합니다.

1차 지원 언어는 다음 두 가지입니다.

- `ko-KR`
- `en-US`

현재 앱은 WinForms Designer가 아니라 코드에서 직접 UI 컨트롤을 생성하므로, 그 구조에 맞는 가벼운 설계를 우선합니다.

## 범위

다국어 적용 대상은 사용자에게 보이는 앱 문구입니다.

- 메인 창 제목, 헤더, 툴바 라벨, 버튼, 콤보박스 표시명, 그리드 컬럼명, 상태바 문구
- Activity 영역 라벨, Activity 필터, 선택된 pair 상태 문구
- `Copy changes`, `Mirror source` 같은 sync mode 표시명
- 필터 팝업 제목, 도움말, 필드 라벨, 버튼, 앱이 관리하는 프리셋 tooltip
- 폴더 선택 창 설명
- 앱이 생성하는 Activity 로그 문구
- 앱이 생성하는 검증/오류 문구

1차 범위에서 제외할 항목은 다음과 같습니다.

- `README.md` 번역. 프로젝트 규칙상 README는 한국어 유지
- 사용자가 입력한 pair 이름, 경로, config 값, 파일명 번역
- 저장되는 mode 값인 `copy`, `mirror` 번역
- 외부 사용자 편집용 번역팩
- RTL 언어 지원

## 사용자 경험

메인 창 툴바의 기존 `Skin` 근처에 `Language` 선택 콤보박스를 둡니다.

언어를 바꾸면 다음처럼 동작합니다.

- 보이는 라벨을 즉시 갱신합니다.
- 선택 언어를 `config.toml`에 저장합니다.
- 기존 sync pair, 로그, 진행률, 스킨, 동기화 간격은 유지합니다.

시작 시 동작은 다음과 같습니다.

- `config.toml`에 `language`가 있으면 그 값을 사용합니다.
- 값이 없거나 지원하지 않는 값이면 우선 `ko-KR`을 기본값으로 사용합니다.
- 나중에 OS 언어 자동 감지를 추가하더라도 localization API는 유지할 수 있게 설계합니다.

## 설정

최상위 설정에 다음 키를 추가합니다.

```toml
language = "ko-KR"
```

규칙:

- `ko-KR`, `en-US`만 지원합니다.
- 지원하지 않는 값은 `ko-KR`로 fallback 합니다.
- 기존 config에 `language`가 없어도 정상 로드됩니다.
- config 저장 시 선택된 언어를 기록합니다.

## 아키텍처

WinForms `.resx` Designer 리소스보다 가벼운 커스텀 localization service를 사용합니다.

이유:

- 현재 UI가 `MainForm.cs`, `FilterDialog.cs`에서 코드로 직접 생성됩니다.
- 대부분의 문자열이 Designer metadata가 아니라 코드에서 할당됩니다.
- key 기반 서비스가 현재 구조에서 점진 적용과 테스트에 더 쉽습니다.

### 새 타입

`LanguageOption`

- `Code`: `ko-KR`, `en-US`
- `DisplayName`: 언어 선택 콤보박스에 보일 이름

`LocalizationService`

- 현재 언어 코드를 관리합니다.
- 단순 문자열은 `Text(string key)`로 반환합니다.
- 포맷 문자열은 `Format(string key, params object[] args)`로 반환합니다.
- 번역이 없으면 `ko-KR`, 그래도 없으면 key 자체로 fallback 합니다.
- 지원 언어 목록을 제공합니다.

`LocalizedStrings`

- `ko-KR`, `en-US`용 static dictionary를 둡니다.
- key 이름은 안정적으로 유지하고 영역별로 나눕니다.
  - `app.title`
  - `toolbar.sync_now`
  - `grid.source`
  - `activity.no_targets`
  - `filter.extensions.label`
  - `log.completed`

## UI 적용

### MainForm

private localization service 필드와 language ComboBox를 추가합니다.

`ApplyLanguage()` 메서드를 추가해서 다음 값을 갱신합니다.

- 창 제목
- 헤더 제목/부제
- 툴바 라벨과 버튼
- Skin/Language 라벨
- 그리드 컬럼명
- Activity 헤더와 필터 라벨
- 상태바 문구
- Mode ComboBox 표시명

언어 변경 시 폼 전체를 다시 만들지 않고, 기존 컨트롤의 텍스트를 갱신하고 그리드를 refresh 합니다.

### Mode 표시

저장값은 계속 `copy`, `mirror`를 사용합니다.

화면 표시만 localization에서 가져옵니다.

- `copy`: 한국어 `변경 파일 복사`, 영어 `Copy changes`
- `mirror`: 한국어 `소스 미러링`, 영어 `Mirror source`

### FilterDialog

`LocalizationService` 또는 선택 언어를 dialog에 전달합니다.

번역 대상:

- 팝업 제목
- 상단 설명
- Quick preset 설명
- 필드 라벨, 힌트, tooltip
- `Include subdirectories`
- `Reset`, `Cancel`, `Apply`

프리셋 이름은 config 기반입니다. 사용자가 `config.toml`에서 프리셋 이름을 바꾸면 그 값을 그대로 표시합니다. 앱이 기본 프리셋을 생성할 때만 내장 기본 이름을 언어별로 표시하는 방향을 고려합니다.

## Activity 로그

새로 생성되는 로그 문구는 localization key를 사용합니다.

이미 로그 텍스트 박스에 남아 있는 기존 로그는 언어 변경 시 다시 번역하지 않습니다. 이력 혼선을 줄이고 구현을 단순하게 유지하기 위해서입니다.

예:

- 한국어: `[Pair 1] 완료: 복사 1, 유지 2, 삭제 0, 제외 3, 실패 0`
- 영어: `[Pair 1] Done: copied 1, skipped 2, deleted 0, excluded 3, failed 0`

## 테스트

다음 테스트를 추가합니다.

- 지원하지 않는 언어 fallback
- `AppConfig.Language` 저장/로드 round trip
- 두 언어의 mode 표시명
- 두 언어의 선택 pair 상태 표시
- 누락된 localization key fallback

수동 검증:

- config에 `language`가 없을 때 한국어 기본값 확인
- 영어로 변경 후 툴바, 그리드, 상태바, 필터 팝업, Activity 필터가 바뀌는지 확인
- 재시작 후 영어가 유지되는지 확인
- 한국어로 다시 변경 후 config가 갱신되는지 확인
- 동기화 동작 자체는 바뀌지 않는지 확인

## 마이그레이션

기존 config는 그대로 유효합니다.

다국어 기능 적용 후 처음 저장하면 config에 다음 값이 추가됩니다.

```toml
language = "ko-KR"
```

기존 pair 데이터나 filter preset은 앱의 기존 config 저장 포맷 정리 외에는 변경하지 않아야 합니다.

## 리스크

- 하드코딩 문자열이 남으면 UI가 일부만 번역될 수 있습니다.
- 영어/한국어 문구 길이 차이로 레이아웃 폭 조정이 필요할 수 있습니다.
- 로그 문구 localization은 변경 지점이 많으므로 helper를 두고 점진 적용합니다.
- 경로, 사용자 pair 이름 같은 동적 값은 번역하면 안 됩니다.

## 권장 구현 단계

1. config와 localization service를 테스트와 함께 추가합니다.
2. 메인 창의 정적 UI와 mode 표시를 번역합니다.
3. 상태바와 Activity helper formatting을 번역합니다.
4. 필터 팝업을 번역합니다.
5. 문서와 하네스를 갱신합니다.
6. 사용자 기능 변경이므로 버전을 올립니다.
