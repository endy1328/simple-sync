# simple sync

`simple sync`는 특정 소스 디렉터리에서 타겟 디렉터리로 변경된 파일을 단방향 복사해 주는 간단한 Windows 데스크톱 앱입니다.

현재 버전은 `1.2.1`입니다. 버전 변경 이력은 `CHANGELOG.md`에 기록하고, 현재 배포 버전은 `VERSION` 파일에 저장합니다.

![simple sync - Fluent Light](https://private-user-images.githubusercontent.com/31756669/595345787-bb712b28-88a8-4d24-9b1e-af2e3c5add33.png?jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmF3LmdpdGh1YnVzZXJjb250ZW50LmNvbSIsImtleSI6ImtleTUiLCJleHAiOjE3NzkyNjkwNDcsIm5iZiI6MTc3OTI2ODc0NywicGF0aCI6Ii8zMTc1NjY2OS81OTUzNDU3ODctYmI3MTJiMjgtODhhOC00ZDI0LTliMWUtYWYyZTNjNWFkZDMzLnBuZz9YLUFtei1BbGdvcml0aG09QVdTNC1ITUFDLVNIQTI1NiZYLUFtei1DcmVkZW50aWFsPUFLSUFWQ09EWUxTQTUzUFFLNFpBJTJGMjAyNjA1MjAlMkZ1cy1lYXN0LTElMkZzMyUyRmF3czRfcmVxdWVzdCZYLUFtei1EYXRlPTIwMjYwNTIwVDA5MTkwN1omWC1BbXotRXhwaXJlcz0zMDAmWC1BbXotU2lnbmF0dXJlPWE3M2QxYTk5MzZkMzBjYTJlMzkyYjU2ZTcwNzVhMmYzOTZhYjY1MWJmOTIzZThmZjA1OWY3Njc5ODI4YmZiNzkmWC1BbXotU2lnbmVkSGVhZGVycz1ob3N0JnJlc3BvbnNlLWNvbnRlbnQtdHlwZT1pbWFnZSUyRnBuZyJ9.CP1hX1JQDmpXKxCyWGnHmnUhGCigvdiDJbXIYINDsWo)

![simple sync - Fluent Light](https://private-user-images.githubusercontent.com/31756669/595345535-5d73df9a-d4b4-4d2b-bf97-dcfdb6c7c2cf.png?jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmF3LmdpdGh1YnVzZXJjb250ZW50LmNvbSIsImtleSI6ImtleTUiLCJleHAiOjE3NzkyNjkwNDcsIm5iZiI6MTc3OTI2ODc0NywicGF0aCI6Ii8zMTc1NjY2OS81OTUzNDU1MzUtNWQ3M2RmOWEtZDRiNC00ZDJiLWJmOTctZGNmZGI2YzdjMmNmLnBuZz9YLUFtei1BbGdvcml0aG09QVdTNC1ITUFDLVNIQTI1NiZYLUFtei1DcmVkZW50aWFsPUFLSUFWQ09EWUxTQTUzUFFLNFpBJTJGMjAyNjA1MjAlMkZ1cy1lYXN0LTElMkZzMyUyRmF3czRfcmVxdWVzdCZYLUFtei1EYXRlPTIwMjYwNTIwVDA5MTkwN1omWC1BbXotRXhwaXJlcz0zMDAmWC1BbXotU2lnbmF0dXJlPWI4ODE1ZDYxNjA4ZjZmMDg1NTY5NjQ4NDhiNWFjNmM2NjIyNGVkZWRmN2FlZTk5NWU2OGE5YWVlNzFhMzVkODQmWC1BbXotU2lnbmVkSGVhZGVycz1ob3N0JnJlc3BvbnNlLWNvbnRlbnQtdHlwZT1pbWFnZSUyRnBuZyJ9.geaOOsrd7NNhP0yGQaPUk8kxCJ7UdeBrkL-vzsk4t1Y)

## 주요 기능

- 설정된 소스/타겟 쌍을 지정한 초 단위 간격으로 확인합니다.
- 소스 전체를 훑되, 타겟에 없거나 변경된 파일만 복사하는 기본 모드를 제공합니다.
- 동기화 쌍별로 `Copy changes` 또는 `Mirror source` 모드를 선택할 수 있습니다.
- `A -> B`, `C -> D`처럼 2개 이상의 동기화 쌍을 설정할 수 있습니다.
- 각 동기화 쌍에 이름을 붙이고 Activity 로그에서 이름으로 구분할 수 있습니다.
- 동기화 쌍별로 확장자, 특정 파일, include/exclude 패턴 필터를 설정할 수 있습니다.
- 각 동기화 쌍의 진행률은 `Sync pairs` 목록에서 확인하고, 선택한 쌍의 상태는 Activity 헤더에서 확인할 수 있습니다.
- 현재 처리 중인 파일이나 경로는 `Progress` 셀 툴팁으로 확인할 수 있습니다.
- Activity 로그는 `All`, `Selected`, `Errors` 기준으로 필터링할 수 있습니다.
- `Sync Now` 버튼을 누르면 즉시 동기화를 실행합니다.
- 활성화된 동기화 쌍이 없으면 Activity에 `대상이 없습니다.`를 표시합니다.
- `Add Pair`, `Remove`, `Choose Source`, `Choose Target`, `Edit Filter...` 버튼으로 동기화 쌍을 관리합니다.
- `Add Pair`로 새로 추가한 동기화 쌍은 경로 입력 중 자동 실행되지 않도록 기본 `Off` 상태로 시작합니다.
- `Sync pairs` 목록의 컬럼 폭을 드래그해서 조절할 수 있고, 긴 경로는 가로 스크롤로 확인할 수 있습니다.
- `Skin` 콤보박스에서 내장 스킨을 선택할 수 있습니다.
- `언어` 콤보박스에서 `한국어` 또는 `English`를 선택할 수 있습니다.
- `Sync pairs`와 `Activity` 사이의 구분선을 드래그해 영역 높이를 조절할 수 있습니다.
- 마지막으로 종료한 창 크기를 기억하고 다음 실행 때 같은 크기로 엽니다.
- 하단 상태 영역에 현재 프로그램 버전을 표시합니다.
- 실행 파일 옆의 `config.toml`에서 설정을 불러오고, 화면에서 바뀐 값을 다시 저장합니다.
- 중복 실행을 막아 같은 앱이 여러 개 떠서 파일을 잠그는 상황을 줄입니다.

이 앱은 양방향 동기화를 수행하지 않습니다. `Mirror source` 모드에서는 소스에 없는 타겟 파일을 삭제할 수 있으므로 중요한 타겟 폴더에는 주의해서 사용해야 합니다.

## 다국어 설정

- 상단 툴바의 `언어` 콤보박스에서 `한국어` 또는 `English`를 선택할 수 있습니다.
- 선택한 언어는 `config.toml`의 `language` 값으로 저장되며 다음 실행 시 자동으로 복원됩니다.
- 기존 설정 파일에 `language`가 없어도 실행 가능하며 기본값은 `ko-KR`입니다.
- 내부 동기화 방식 값은 계속 `copy`, `mirror`로 저장되므로 기존 설정과 호환됩니다.

## 실행

프로젝트 폴더에서 실행:

```powershell
dotnet run
```

`dotnet run`은 필요한 경우 자동으로 빌드한 뒤 실행합니다.
Debug 빌드는 설치된 Release 앱과 다른 단일 인스턴스 키를 사용하므로, 설치본을 실행한 상태에서도 개발용 `dotnet run` 앱을 동시에 띄워 비교 테스트할 수 있습니다.
Debug 빌드로 실행한 앱은 창 제목에 `(Debug)`, 하단 상태 영역에 `Debug`를 표시합니다.

컴파일만 확인:

```powershell
dotnet build
```

동기화 엔진 테스트 실행:

```powershell
dotnet run --project .\tests\SimpleSync.Tests\SimpleSync.Tests.csproj
```

Windows x64 Release 빌드 생성:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

배포용 실행 파일 위치:

```text
bin\Release\net10.0-windows\win-x64\publish\simple sync.exe
```

이 빌드는 framework-dependent 방식이므로 대상 PC에 호환되는 .NET Windows Desktop Runtime이 필요합니다.

## exe 실행이 제한된 환경

SI 현장처럼 사용자 제작 exe 실행이 제한된 환경을 위해 `scripts` 폴더에 fallback 스크립트를 제공합니다.

PowerShell fallback:

```powershell
.\scripts\simple-sync.ps1 -ConfigPath .\config.toml
```

1회만 실행:

```powershell
.\scripts\simple-sync.ps1 -ConfigPath .\config.toml -Once
```

이 스크립트는 `config.toml`의 `interval_seconds`와 `[[pairs]]` 설정을 읽고 Windows 기본 도구인 `robocopy`를 호출합니다.
`mode = "copy"`는 robocopy `/E`, `mode = "mirror"`는 robocopy `/MIR`로 실행됩니다.
앱의 세부 필터 규칙은 Windows Forms 앱 엔진 기준으로 동작합니다. PowerShell fallback은 현재 `include`, `exclude`, `extensions`, `files`, `include_subdirectories`를 적용하지 않으므로 필터가 필요한 경우 앱 실행을 권장합니다.

비상용 robocopy cmd 예제:

```cmd
scripts\simple-sync-robocopy.cmd "\\wsl.localhost\Ubuntu-24.04\home\u24\projects\spring-lean\docs" "C:\프로젝트 자료\GSNext Phase2\01. 준비"
```

`simple-sync-robocopy.cmd`는 경로를 인자로 받는 최소 예제입니다. 인자를 생략하면 파일 안의 fallback `SOURCE`, `TARGET` 값을 사용합니다.

## 설치 프로그램 생성

Windows 설치 프로그램은 Inno Setup 기반으로 생성합니다.
소스 코드, 아이콘, 스크립트, README, `config.example.toml` 등 설치본에 포함되는 파일이 변경되면 아래 명령을 다시 실행해 `dist\simple-sync-setup.exe`를 새로 생성해야 합니다.

사전 준비:

- Inno Setup 6 설치
- `ISCC.exe`가 PATH에 있거나 기본 설치 경로에 있어야 합니다.

설치용 self-contained staging 생성:

```powershell
.\scripts\publish-installer.ps1 -SkipInno
```

설치 프로그램 생성:

```powershell
.\scripts\publish-installer.ps1
```

소스 수정 후 설치본을 갱신할 때도 동일하게 아래 명령만 실행하면 됩니다.

```powershell
.\scripts\publish-installer.ps1
```

이 스크립트 안에서 설치본용 `dotnet publish`와 Inno Setup 컴파일을 함께 실행합니다.
설치 파일 버전은 `VERSION` 파일의 값을 사용합니다.
따라서 설치본 생성을 위해 `dotnet publish -c Release -r win-x64 --self-contained false`를 별도로 먼저 실행할 필요는 없습니다.

사전 확인만 하고 싶다면 먼저 빌드만 실행할 수 있습니다.

```powershell
dotnet build
```

생성 결과:

```text
dist\simple-sync-setup.exe
```

설치 프로그램은 `.NET Windows Desktop Runtime`이 없는 PC에서도 실행되도록 self-contained publish 결과를 포함합니다.
기존 `dist\simple-sync-setup.exe`가 있어도 스크립트가 최신 publish 결과로 덮어써서 다시 만듭니다.

## 설정 파일

`config.toml`은 `simple sync.exe`와 같은 폴더에 저장됩니다. 앱은 시작 시 이 파일을 읽고, 화면에서 설정이 변경되거나 앱이 종료될 때 최신 값을 저장합니다.

저장소에는 개인 경로가 들어가는 실제 `config.toml` 대신 `config.example.toml` 예제 파일만 포함합니다.

## 설정 예시

```toml
interval_seconds = 10
skin = "syncback_blue"
window_width = 1720
window_height = 1120

[[filter_presets]]
name = "Documents"
extensions = [".docx", ".xlsx", ".pdf", ".txt", ".md"]

[[filter_presets]]
name = "Images"
extensions = [".png", ".jpg", ".svg"]

[[filter_presets]]
name = "Code"
extensions = [".cs", ".js", ".json", ".xml", ".md"]

[[filter_presets]]
name = "Archives"
extensions = [".zip", ".7z", ".tar"]

[[filter_presets]]
name = "Exclude temp/build"
exclude = ["*.tmp", ".git/**", "bin/**", "obj/**", "node_modules/**"]

[[pairs]]
name = "Main Backup"
enabled = true
mode = "copy"
source = "C:\\source"
target = "D:\\backup"
extensions = [".md", ".pdf"]
files = ["README.md", "docs/setup.md"]
include = ["docs/**"]
exclude = ["bin/**", "obj/**", ".git/**", "*.tmp"]
include_subdirectories = true
```

## 스킨

사용자 이미지 스킨은 지원하지 않고, 코드에 정의된 내장 스킨만 제공합니다.

현재 내장 스킨:

- `Fluent Light`
- `SyncBack Blue`
- `Graphite Dark`
- `Warm Folder`
- `Soft Mint`

마지막으로 선택한 스킨은 `config.toml`에 저장되며 다음 실행 시 자동으로 적용됩니다.

## 동기화 규칙

- 활성화된 동기화 쌍만 처리합니다.
- 동기화 로그는 `[Pair Name]` prefix와 `Source -> Target` 진행경로로 표시됩니다.
- 각 동기화 쌍의 `Mode`는 기본값 `Copy changes`입니다.
- `Copy changes` 모드는 타겟에 없거나 변경된 파일만 복사하고, 타겟에만 있는 파일은 유지합니다.
- `Mirror source` 모드는 복사 후 소스에 없는 타겟 파일과 디렉터리를 삭제해 두 폴더의 파일 구성을 맞춥니다.
- 필터가 설정된 경우 필터에 포함된 파일만 동기화 대상입니다.
- `extensions`, `files`, `include`는 OR 조건으로 포함 대상을 정하고, `exclude`는 항상 우선 적용됩니다.
- `files`는 소스 폴더 기준의 정확한 상대 경로입니다. 예를 들어 `C:\source\docs\setup.md`는 `docs/setup.md`로 입력합니다.
- `include`와 `exclude`는 glob 패턴입니다. 예: `docs/**`, `reports/*.pdf`, `*.tmp`.
- 필터 밖 파일은 `Copy changes`에서도 복사하지 않고, `Mirror source`에서도 삭제하지 않습니다.
- `include_subdirectories = false`이면 소스 루트 직속 파일만 검사합니다.
- `[[filter_presets]]` 항목을 수정하면 `Edit Filter...` 팝업의 프리셋 버튼 이름과 자동 입력 값을 바꿀 수 있습니다.
- 프리셋 버튼은 여러 개를 눌러 함께 사용할 수 있고, 이미 입력된 값은 중복 추가하지 않습니다.
- 기존 `config.toml`에 `mode`가 없으면 자동으로 `copy`로 처리합니다.
- 소스와 타겟 경로가 모두 비어 있는 동기화 쌍은 저장하지 않고, 다음 실행 시 표시하지 않습니다.
- 타겟 디렉터리가 없으면 자동으로 생성합니다.
- 타겟에 파일이 없으면 복사합니다.
- 타겟 파일과 소스 파일의 크기가 다르면 복사합니다.
- 파일 크기가 같아도 마지막 수정 시간이 1초 이상 다르면 복사합니다.
- 파일은 임시 파일에 청크 단위로 먼저 복사하고, 성공한 뒤 타겟 파일로 교체합니다.
- 복사 중에는 현재 파일의 바이트 진행률을 보고하고, 파일 단위 일시 오류는 짧게 재시도합니다.
- Activity 완료 로그에는 복사, 유지, 삭제, 제외, 실패 개수를 표시합니다.
- 복사 후 타겟 파일의 마지막 수정 시간을 소스 파일과 맞춥니다.
- `Mirror source` 모드에서도 소스 탐색이나 복사 중 실패가 있으면 안전을 위해 삭제 단계는 건너뜁니다.
- 타겟 경로가 소스와 같거나 소스 내부인 경우 재귀 복사를 막기 위해 건너뜁니다.
- 파일 시스템 오류는 앱 로그에 표시되며, 가능한 경우 다른 파일과 다른 동기화 쌍 처리는 계속합니다.
- 앱 종료 시 자동 실행 타이머를 멈추고 진행 중인 동기화에 취소 신호를 보냅니다.

현재 변경 감지는 파일 내용 해시가 아니라 파일 크기와 마지막 수정 시간 기준입니다.

## Git 포함 기준

Git에 포함:

- 앱 소스 코드
- `README.md`
- `CHANGELOG.md`
- `VERSION`
- `config.example.toml`
- `.gitignore`
- `scripts/simple-sync.ps1`
- `scripts/simple-sync-robocopy.cmd`
- `scripts/publish-installer.ps1`
- `installer/simple-sync.iss`
- `tests/SimpleSync.Tests`
- 아이콘 파일과 아이콘 생성 스크립트
- AI/하네스/인수인계 문서

Git에서 제외:

- `bin/`
- `obj/`
- `artifacts/`
- 실제 실행 설정인 `config.toml`
- Visual Studio 사용자 파일
- 로그/임시 파일
- 검증용 추출 이미지 `Assets/extracted-app-icon.png`

## 프로젝트 문서

- `AGENT.md`: 프로젝트 작업 에이전트를 위한 영문 운영 규칙
- `AGENT_kor.md`: 에이전트 운영 규칙 한국어 버전
- `HARNESS.md`: 영문 빌드 및 검증 하네스 문서
- `HARNESS_kor.md`: 빌드 및 검증 하네스 한국어 버전
- `이어서 개발할때.md`: 다른 세션에서 이어서 개발하기 위한 한국어 인수인계 문서
