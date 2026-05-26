# 변경 이력

`simple sync`의 주요 변경 사항을 기록합니다.

## 1.1.0 - 2026-05-26

- 대용량 파일 복사 안정성을 개선했습니다. 임시 파일에 먼저 복사한 뒤 성공 시 타겟 파일로 교체합니다.
- `Sync pairs` 목록에 `Progress`, `Current` 컬럼을 추가했습니다.
- Activity 로그에 `All`, `Selected`, `Errors` 필터를 추가했습니다.
- progress, copy mode, mirror mode를 검증하는 콘솔 테스트를 추가했습니다.
- Inno Setup 기반 설치 파일 생성 워크플로를 추가했습니다.
- UI 레이아웃, 스킨, 아이콘, 단일 인스턴스 실행, 창 크기 복원을 개선했습니다.
- 개발 편의를 위해 Debug 실행은 설치된 Release 앱과 별도의 단일 인스턴스 키를 사용하도록 했습니다.
- 기본 `Copy changes` 모드와 별도로 `Mirror source` 모드를 추가했습니다.
- 앱 화면과 설치 파일에 버전 `1.1.0`을 표시하도록 정리했습니다.

## 1.0.0 - 2026-05-20

- Windows Forms 기반 단방향 폴더 동기화 앱의 초기 안정 버전입니다.
- 여러 source/target sync pair를 지원했습니다.
- 초 단위 자동 동기화와 수동 `Sync Now`를 지원했습니다.
- 설정을 `config.toml`에 저장했습니다.
- source에서 target으로 신규/변경 파일을 복사했습니다.
