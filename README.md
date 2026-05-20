# simple sync

`simple sync`는 특정 소스 디렉터리에서 타겟 디렉터리로 변경된 파일을 단방향 복사해 주는 간단한 Windows 데스크톱 앱입니다.

## 주요 기능

- 설정된 소스/타겟 쌍을 지정한 초 단위 간격으로 확인합니다.
- 새 파일 또는 변경된 파일만 소스에서 타겟으로 복사합니다.
- `A -> B`, `C -> D`처럼 2개 이상의 동기화 쌍을 설정할 수 있습니다.
- `Sync Now` 버튼을 누르면 즉시 동기화를 실행합니다.
- 실행 파일 옆의 `config.toml`에서 설정을 불러오고, 화면에서 바뀐 값을 다시 저장합니다.

이 앱은 타겟 파일을 삭제하지 않으며, 양방향 동기화도 수행하지 않습니다.

## 실행

프로젝트 폴더에서 빌드 후 실행:

```powershell
dotnet run
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

스킨은 화면의 `Skin` 콤보박스에서 선택할 수 있으며, 마지막 선택값은 `config.toml`에 저장됩니다.

## 설정 예시

```toml
interval_seconds = 10
skin = "syncback_blue"

[[pairs]]
enabled = true
source = "C:\\source"
target = "D:\\backup"
```

## 동기화 규칙

- 활성화된 동기화 쌍만 처리합니다.
- 소스와 타겟 경로가 모두 비어 있는 동기화 쌍은 저장하지 않고, 다음 실행 시 표시하지 않습니다.
- 타겟 디렉터리가 없으면 자동으로 생성합니다.
- 타겟에 파일이 없거나, 파일 크기 또는 마지막 수정 시간이 다르면 복사합니다.
- 복사 후 타겟 파일의 마지막 수정 시간을 소스 파일과 맞춥니다.
- 타겟 경로가 소스와 같거나 소스 내부인 경우 재귀 복사를 막기 위해 건너뜁니다.
- 파일 시스템 오류는 앱 로그에 표시되며, 가능한 경우 다른 파일과 다른 동기화 쌍 처리는 계속합니다.

## 프로젝트 문서

- `AGENT.md`: 프로젝트 작업 에이전트를 위한 영문 운영 규칙
- `AGENT_kor.md`: 에이전트 운영 규칙 한국어 버전
- `HARNESS.md`: 영문 빌드 및 검증 하네스 문서
- `HARNESS_kor.md`: 빌드 및 검증 하네스 한국어 버전
