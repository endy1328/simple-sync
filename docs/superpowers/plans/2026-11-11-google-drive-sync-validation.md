# simple sync 1.3.0 검증 기록

검증일: 2026-11-11

## 검증 범위

- 콘솔 테스트: 기존 로컬 회귀 테스트와 새 계정·Drive HTTP·설정·공통 엔진·STA 화면 테스트. 총 37개 상위 테스트 그룹이며 각 그룹 내부에 여러 사례가 있다.
- Drive HTTP 테스트: 전체 페이지 조회, 잘못된·불완전한 목록, 세 종류 Office 내보내기, 소유권과 루트 경계, 영구 DELETE, 비어 있지 않은 폴더 보존, 원래 형식 업로드, 기존 ID 갱신, 0바이트, 다중 청크, 응답 유실·호출 제한 복구.
- 공통 엔진: 소스 우선, 해시 비교, 수정 시각 보존, 필터 제외 보존, 조회·전송·충돌·취소·실행 중 소스 변경 후 삭제 중단, 내보내기 재사용, 확장 경로를 이용한 중첩 차단.
- UI: 한국어·영어 계정·연결 화면 렌더링, 방향 전환, 취소 시 초안 격리, 비동기 폴더 조회 취소, 동기화 중 사용자 편집 차단.
- 실제 Google OAuth 로그인과 클라우드 데이터 전송은 수행하지 않았다. 사용자의 OAuth 클라이언트 설정과 브라우저 승인이 필요하다.

## 실행 명령

```powershell
dotnet run --project tests\SimpleSync.Tests\SimpleSync.Tests.csproj
dotnet build -c Release
.\scripts\publish-installer.ps1
git diff --check
```

## 배포 확인 방법

VERSION, 프로젝트 Version/AssemblyVersion/FileVersion/InformationalVersion, 설치 프로그램 기본 버전, README, CHANGELOG, release-notes-1.3.0.md를 함께 갱신했다. 생성한 staging 실행 파일·DLL과 setup 실행 파일의 FileVersionInfo 및 staging/VERSION을 대조한다.

설치 빌드에서 NuGet 취약성 데이터 원본(api.nuget.org)에 연결하지 못할 경우 NU1900 경고가 발생할 수 있다. 이는 취약성 조회 미완료이며 실제 취약성이 없음을 의미하지 않는다. 검증 결과를 보고할 때 컴파일 실패와 구분한다.

## 최종 결과

- 콘솔 테스트: 37개 그룹 통과, 종료 코드 0.
- Release 빌드: 경고 0개, 오류 0개, 종료 코드 0.
- 최신 소스의 설치 파일 재생성: Inno Setup 컴파일 성공, 종료 코드 0. 설치용 publish 중 NuGet 취약성 데이터 조회 연결 실패로 NU1900 경고가 발생했다.
- staging의 앱 EXE와 DLL: FileVersion 1.3.0.0, ProductVersion 1.3.0.
- setup EXE: ProductVersion 1.3.0. staging/VERSION도 1.3.0으로 일치.
- PowerShell fallback: Drive 쌍을 robocopy에 전달하지 않고 명시적으로 건너뛰는 동작 확인.
- git diff --check 통과. Git의 LF→CRLF 안내는 공백 오류와 구분한다.
- 설치 프로그램 실행을 통한 실제 설치와 실제 Google 계정 전송은 수행하지 않았다.
