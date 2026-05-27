# 하네스 가이드

이 문서는 `simple sync`의 빌드, 실행, 배포, 검증 방법을 설명합니다.
`HARNESS.md`가 변경되면 같은 변경에서 이 파일도 함께 업데이트합니다.

## 환경

- Windows
- Windows Desktop 지원이 포함된 .NET SDK
- 대상 프레임워크: `net10.0-windows`

## 빌드

```powershell
dotnet build
```

기대 결과: 오류 없이 빌드가 성공합니다.

## 테스트

```powershell
dotnet run --project .\tests\SimpleSync.Tests\SimpleSync.Tests.csproj
```

기대 결과: 콘솔 검증이 모두 `PASS`로 출력됩니다.

## 실행

```powershell
dotnet run
```

`simple sync (Debug)` 제목의 Windows Forms UI가 열립니다.
Debug 실행은 Release와 다른 단일 인스턴스 Mutex를 사용하므로 설치된 Release 앱과 개발용 `dotnet run` 앱을 동시에 띄워 비교할 수 있습니다. Debug 실행은 창 제목에 `(Debug)`, 하단 상태 영역에 `Debug`를 표시합니다.

## 배포

현재 배포 버전은 `VERSION` 파일에 있습니다. `scripts\publish-installer.ps1`는 설치 파일을 만들 때 이 값을 읽습니다.

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

기대 산출물:

```text
bin\Release\net10.0-windows\win-x64\publish\simple sync.exe
```

## 수동 검증 체크리스트

- 앱을 시작하고 기존 `config.toml` 값이 로드되는지 확인합니다.
- 설치된 Release 앱을 먼저 실행한 상태에서 `dotnet run`을 실행하고 Debug 앱이 나란히 열리는지 확인합니다.
- Debug 앱 제목이 `simple sync (Debug)`로 표시되고 하단 상태 영역에 `Debug`가 포함되는지 확인합니다.
- 활성화된 동기화 쌍을 최소 2개 추가하고 둘 다 저장되는지 확인합니다.
- `Add Pair`를 클릭하고 새 동기화 쌍의 `On` 컬럼이 체크 해제 상태인지 확인합니다.
- `Edit Filter...`로 필터를 설정하고 `Filter` 요약 컬럼과 툴팁에 규칙이 표시되는지 확인합니다.
- 필터 프리셋 버튼 여러 개를 클릭하고 버튼이 활성 표시를 유지하며, 누른 순서대로 값이 추가되고 중복 입력되지 않는지 확인합니다.
- `Now`를 클릭하고 변경 파일이 소스에서 타겟으로 복사되는지 확인합니다.
- 모든 쌍을 비활성화한 뒤 `Sync Now`를 클릭하고 시작 메시지 다음에 Activity에 `대상이 없습니다.`가 표시되는지 확인합니다.
- 동기화 실행 중 `Progress` 컬럼이 갱신되고, 선택한 쌍의 짧은 상태가 Activity 헤더에 표시되며, 현재 파일/경로가 `Progress` 툴팁에 표시되는지 확인합니다.
- Activity 로그를 `All`, `Selected`, `Errors`로 필터링할 수 있는지 확인합니다.
- 다음 실행에서 변경 없는 파일이 건너뛰어지는지 확인합니다.
- 기본 `Copy changes` 모드에서 타겟에만 있는 파일이 유지되는지 확인합니다.
- 한 쌍을 `Mirror source`로 설정하고, 소스 파일 복사가 성공한 뒤 타겟에만 있는 파일이 삭제되는지 확인합니다.
- 필터가 설정된 mirror 쌍에서 필터 밖 타겟 전용 파일이 보존되는지 확인합니다.
- 한 쌍을 비활성화하고 처리되지 않는지 확인합니다.
- 존재하지 않는 소스 경로를 설정하고 앱이 문제를 로그로 남기는지 확인합니다.
- 타겟 경로를 소스 내부로 설정하고 앱이 건너뛰는지 확인합니다.
- 간격 값을 변경하고 `config.toml`에 저장되는지 확인합니다.
- pair 모드를 변경하고 `config.toml`에 `mode = "copy"` 또는 `mode = "mirror"`가 저장되는지 확인합니다.
- 필터 규칙을 변경하고 `config.toml`에 `include`, `exclude`, `extensions`, `files`, `include_subdirectories`가 저장되는지 확인합니다.
- `config.toml`의 `[[filter_presets]]` 항목을 수정하고 필터 팝업이 변경된 프리셋 버튼/값을 사용하는지 확인합니다.

## 다국어 수동 확인

1. `config.toml`에 `language` 키가 없는 상태로 앱을 실행하고 한국어가 선택되는지 확인합니다.
2. 언어를 English로 변경하고 툴바, 그리드 헤더, 상태 텍스트, Activity 필터, 필터 팝업 문구가 즉시 변경되는지 확인합니다.
3. 앱을 닫았다가 다시 실행하고 English가 복원되는지 확인합니다.
4. 다시 한국어로 변경하고 `language = "ko-KR"`이 저장되는지 확인합니다.
5. 두 언어에서 동기화를 실행해 파일 복사 동작이 변경되지 않았는지 확인합니다.

## 설정 위치

실행 시 `config.toml`은 실행 파일 옆에 있습니다. 개발 중에는 보통 다음 위치입니다.

```text
bin\Debug\net10.0-windows\config.toml
```

배포 빌드에서는 보통 다음 위치입니다.

```text
bin\Release\net10.0-windows\win-x64\publish\config.toml
```
