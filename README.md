# simple sync

`simple sync`는 특정 소스 디렉터리에서 타겟 디렉터리로 변경된 파일을 단방향 복사해 주는 간단한 Windows 데스크톱 앱입니다.

## 주요 기능

- 설정된 소스/타겟 쌍을 지정한 초 단위 간격으로 확인합니다.
- 소스 전체를 훑되, 타겟에 없거나 변경된 파일만 복사합니다.
- `A -> B`, `C -> D`처럼 2개 이상의 동기화 쌍을 설정할 수 있습니다.
- `Sync Now` 버튼을 누르면 즉시 동기화를 실행합니다.
- `Add Pair`, `Remove`, `Choose Source`, `Choose Target` 버튼으로 동기화 쌍을 관리합니다.
- `Skin` 콤보박스에서 내장 스킨을 선택할 수 있습니다.
- 실행 파일 옆의 `config.toml`에서 설정을 불러오고, 화면에서 바뀐 값을 다시 저장합니다.

이 앱은 타겟 파일을 삭제하지 않으며, 양방향 동기화도 수행하지 않습니다.

## 실행

프로젝트 폴더에서 실행:

```powershell
dotnet run
```

`dotnet run`은 필요한 경우 자동으로 빌드한 뒤 실행합니다.

컴파일만 확인:

```powershell
dotnet build
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

## 설정 파일

`config.toml`은 `simple sync.exe`와 같은 폴더에 저장됩니다. 앱은 시작 시 이 파일을 읽고, 화면에서 설정이 변경되거나 앱이 종료될 때 최신 값을 저장합니다.

저장소에는 개인 경로가 들어가는 실제 `config.toml` 대신 `config.example.toml` 예제 파일만 포함합니다.

## 설정 예시

```toml
interval_seconds = 10
skin = "syncback_blue"

[[pairs]]
enabled = true
source = "C:\\source"
target = "D:\\backup"
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
- 소스와 타겟 경로가 모두 비어 있는 동기화 쌍은 저장하지 않고, 다음 실행 시 표시하지 않습니다.
- 타겟 디렉터리가 없으면 자동으로 생성합니다.
- 타겟에 파일이 없으면 복사합니다.
- 타겟 파일과 소스 파일의 크기가 다르면 복사합니다.
- 파일 크기가 같아도 마지막 수정 시간이 1초 이상 다르면 복사합니다.
- 복사 후 타겟 파일의 마지막 수정 시간을 소스 파일과 맞춥니다.
- 타겟 경로가 소스와 같거나 소스 내부인 경우 재귀 복사를 막기 위해 건너뜁니다.
- 파일 시스템 오류는 앱 로그에 표시되며, 가능한 경우 다른 파일과 다른 동기화 쌍 처리는 계속합니다.

현재 변경 감지는 파일 내용 해시가 아니라 파일 크기와 마지막 수정 시간 기준입니다.

## Git 포함 기준

Git에 포함:

- 앱 소스 코드
- `README.md`
- `config.example.toml`
- `.gitignore`
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
