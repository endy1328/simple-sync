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

## 실행

```powershell
dotnet run
```

`simple sync` 제목의 Windows Forms UI가 열립니다.

## 배포

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

기대 산출물:

```text
bin\Release\net10.0-windows\win-x64\publish\simple sync.exe
```

## 수동 검증 체크리스트

- 앱을 시작하고 기존 `config.toml` 값이 로드되는지 확인합니다.
- 활성화된 동기화 쌍을 최소 2개 추가하고 둘 다 저장되는지 확인합니다.
- `Now`를 클릭하고 변경 파일이 소스에서 타겟으로 복사되는지 확인합니다.
- 다음 실행에서 변경 없는 파일이 건너뛰어지는지 확인합니다.
- 기본 `Copy changes` 모드에서 타겟에만 있는 파일이 유지되는지 확인합니다.
- 한 쌍을 `Mirror source`로 설정하고, 소스 파일 복사가 성공한 뒤 타겟에만 있는 파일이 삭제되는지 확인합니다.
- 한 쌍을 비활성화하고 처리되지 않는지 확인합니다.
- 존재하지 않는 소스 경로를 설정하고 앱이 문제를 로그로 남기는지 확인합니다.
- 타겟 경로를 소스 내부로 설정하고 앱이 건너뛰는지 확인합니다.
- 간격 값을 변경하고 `config.toml`에 저장되는지 확인합니다.
- pair 모드를 변경하고 `config.toml`에 `mode = "copy"` 또는 `mode = "mirror"`가 저장되는지 확인합니다.

## 설정 위치

실행 시 `config.toml`은 실행 파일 옆에 있습니다. 개발 중에는 보통 다음 위치입니다.

```text
bin\Debug\net10.0-windows\config.toml
```

배포 빌드에서는 보통 다음 위치입니다.

```text
bin\Release\net10.0-windows\win-x64\publish\config.toml
```
