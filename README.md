# simple sync

`simple sync`는 로컬·네트워크 폴더 또는 개인 Google Drive의 내 드라이브 사이에서 변경된 파일을 단방향 복사하는 Windows 데스크톱 앱입니다.

현재 버전은 `1.6.3`입니다. 버전 변경 이력은 `CHANGELOG.md`에 기록하고, 현재 배포 버전은 `VERSION` 파일에 저장합니다.

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
- `설정 → 스킨` 메뉴에서 내장 스킨을 선택할 수 있습니다.
- `설정 → 언어` 메뉴에서 `한국어` 또는 `English`를 선택할 수 있습니다.
- `Sync pairs`와 `Activity` 사이의 구분선을 드래그해 영역 높이를 조절할 수 있습니다.
- 마지막으로 종료한 창 크기를 기억하고 다음 실행 때 같은 크기로 엽니다.
- ‘자동’ 체크 상태를 변경 즉시 저장하고, 다음 실행에도 자동 동기화의 켜짐·꺼짐 상태를 유지합니다.
- 하단 상태 영역에 현재 프로그램 버전을 표시합니다.
- 실행 파일 옆의 `config.toml`에서 설정을 불러오고, 화면에서 바뀐 값을 다시 저장합니다.
- 중복 실행을 막아 같은 앱이 여러 개 떠서 파일을 잠그는 상황을 줄입니다.

이 앱은 양방향 동기화를 수행하지 않습니다. `Mirror source` 모드에서는 소스에 없는 타겟 파일을 휴지통 없이 영구 삭제합니다. 필터에서 제외한 파일은 보존하고, 전체 목록 조회와 전송이 성공한 경우에만 삭제 단계를 실행합니다. 타겟이 더 최신이어도 내용이 다르면 소스 기준으로 덮어씁니다.

## Google Drive 연결

개인 Google 계정을 여러 개 연결할 수 있습니다. 지원 방향은 `PC → Google Drive`와 `Google Drive → PC`이며, 계정 간 Drive 전송과 조직 공유 드라이브는 지원하지 않습니다.

### 계정 연결

1. **설정 → 구글 계정 → Google 계정 연결**을 누릅니다.
2. 기본 브라우저에서 사용할 개인 Google 계정을 선택하고 Drive 접근 권한을 승인합니다.
3. 앱으로 돌아와 동기화 쌍의 **연결 설정**에서 계정과 폴더를 선택합니다. 여러 계정은 각각 연결합니다.

일반 사용자는 Google Cloud 프로젝트를 생성하거나 OAuth JSON을 가져올 필요가 없습니다. 기존에 연결한 계정과 인증 설정은 그대로 사용합니다. 사용자 지정 OAuth 클라이언트를 사용하려면 **고급 설정 → OAuth 설정 가져오기**를 사용합니다. 연결된 계정이 있으면 다른 클라이언트로 교체하기 전에 연결을 해제해야 합니다.

### 배포자의 Google 로그인 설정 (최초 한 번)

배포자는 [Google Cloud Console](https://console.cloud.google.com/)에서 Google Drive API를 활성화하고 외부 사용자용 동의 화면과 **Desktop app** OAuth 클라이언트를 준비합니다. 다운로드한 JSON을 저장소 루트의 `google-oauth-client.local.json`에 저장하면 빌드 시 앱 리소스에 자동 포함됩니다. 다른 위치는 `dotnet build -p:GoogleOAuthClientConfigPath="C:\경로\client.json"`로 지정할 수 있습니다. 이 파일은 Git에서 제외됩니다. OAuth 데스크톱 클라이언트 설정만 포함하며 사용자 계정 토큰은 포함하지 않습니다.

설정 없이 개발 빌드는 가능하지만 Google 계정 연결을 사용할 수 없습니다. 설정 없는 배포를 막기 위해 `dotnet publish`는 해당 파일이 없으면 실패합니다. 테스트 프로젝트를 사용할 때도 같은 MSBuild 속성으로 배포자 설정 경로를 전달할 수 있습니다.

배포된 기본 설정은 첫 실행 시 Windows 사용자별 암호화 저장소에 보관합니다. 이후 앱의 기본 클라이언트가 바뀌어도 기존 인증 토큰은 처음 승인받은 클라이언트를 유지합니다. 자체 클라이언트로 이전하려면 기존 계정 연결을 해제하고 고급 설정으로 교체한 뒤 다시 연결합니다.

앱은 기존 폴더 전체 조회·전송·영구 삭제에 필요한 `drive` 권한과 계정 식별용 `openid email`을 요청합니다. `drive`는 제한된 OAuth 권한입니다. 공개 배포용 OAuth 프로젝트는 구글의 검증 요건을 충족해야 하며, 테스트 상태에서는 승인 수명 등 개발용 제약이 적용될 수 있습니다. [권한 범위 안내](https://developers.google.com/workspace/drive/api/guides/api-specific-auth), [데스크톱 OAuth 안내](https://developers.google.com/identity/protocols/oauth2/native-app)

### 동기화 쌍 설정

반복 동기화에서는 계정·루트·필터별 Drive 목록과 변경 커서를 저장해 변경된 파일의 상위 폴더만 다시 조회합니다. PC의 로컬 NTFS 파일은 파일 ID와 변경 시각 등 신원이 같으면 MD5를 재사용합니다. 폴더 구조 변경·캐시 손상·7일 경과 등에는 전체 검사로 복구하며, 미러링 삭제 후보가 있으면 소스를 캐시 없이 다시 검증합니다. 자세한 동작과 모의 측정 결과는 [반복 동기화 설계 문서](docs/google-drive-incremental-1.6.3.md)를 참고하세요.

1. **쌍 추가**로 비활성 동기화 쌍을 만듭니다.
2. 해당 행을 선택하고 **연결 설정**에서 방향, 계정, PC 폴더와 Drive 폴더를 선택합니다. Drive 폴더 선택기는 내 드라이브에서 하위 폴더를 탐색합니다. 새 폴더가 필요하면 부모 폴더를 선택한 뒤 **새 폴더**에서 이름을 입력해 만듭니다. 생성된 폴더가 자동 선택되면 **이 폴더 선택**을 누릅니다.
3. 적용 후 기존 목록에서 copy 또는 mirror 모드와 파일 필터를 설정하고 **On**을 켭니다. 연결 대상이 바뀌면 쌍은 다시 비활성화됩니다.
4. **지금 동기화**로 실행합니다. **동기화 중지**로 현재 실행을 취소할 수 있습니다. 자동 실행 간격은 기존 상단 설정을 공유하며 같은 작업을 중복 실행하지 않습니다.

일반 파일은 원래 형식으로 전송합니다. Google 문서는 `.docx`, 스프레드시트는 `.xlsx`, 프레젠테이션은 `.pptx`로 내려받습니다. PC의 Office 파일을 업로드할 때는 Google 문서로 변환하지 않습니다. 표준 문서 내보내기의 변환 결과는 10MB 제한이 있으며, 실패하면 기존 PC 파일을 유지하고 해당 실행의 미러링 삭제를 생략합니다. [공식 내보내기 제한](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/export)

Drive의 중복 이름, 내보내기 후 파일명 충돌, Windows에서 표현할 수 없는 이름과 파일·폴더 충돌은 오류로 보고하고 삭제 단계를 실행하지 않습니다. 필터에 포함된 미지원 Google 문서 형식과 바로가기도 오류로 표시합니다. 로컬 심볼릭 링크·재분석 지점은 따라가지 않습니다. 미러링의 Drive 영구 삭제는 본인 소유이며 삭제 권한이 있는 파일에 적용할 수 있습니다.

### 작은 파일이 많은 경우

Drive와 전송하는 1MiB 이하 일반 파일은 최대 4개씩 병렬 처리합니다. 이미 존재하는 폴더의 반복 준비를 생략하고, 작은 파일은 메타데이터와 내용을 한 요청으로 업로드합니다. 생성용 파일 ID도 묶음으로 예약해 작은 파일마다 반복하는 통신을 줄입니다.

폴더·파일 목록은 매 실행 다시 확인하며, 실제 쓰기 직전에는 이름 충돌과 부모 경로를 확인합니다. 업로드 결과의 ID·경로·크기·내용 해시를 검증합니다. 대용량 파일은 기존 재개 가능한 전송을 유지하고, Google 문서 내보내기와 로컬 간 전송은 순차 처리합니다. 미러링 삭제는 모든 전송이 종료되고 오류가 없으며 소스 재확인이 성공한 경우에만 시작합니다.

### 인증 정보와 설정

- 폴더·모드·필터·계정 참조는 기존 `config.toml`에 저장합니다. Drive 폴더는 이름이 아닌 ID로 연결됩니다.
- 인증 정보는 `%LOCALAPPDATA%\SimpleSync\GoogleDrive` 아래에 Windows 사용자 기준 DPAPI로 암호화해 저장합니다. Debug 실행은 `GoogleDrive.Debug`를 사용합니다.
- 인증 토큰은 설정 파일이나 로그에 기록하지 않습니다. 다른 Windows 사용자에게 인증 파일을 복사해 사용할 수 없습니다.
- Google 문서의 성공한 내보내기 상태는 `%LOCALAPPDATA%\SimpleSync\SyncState`에 저장합니다. 파일 내용은 이 상태 파일에 포함하지 않습니다.
- 연결 해제 시 동기화 쌍은 남지만 재연결 전에는 실행되지 않습니다. 다른 계정으로 자동 전환하지 않습니다.
- Google Drive 데스크톱 앱 설치는 필요하지 않습니다. PowerShell fallback은 Drive 동기화 쌍을 건너뜁니다.

## 다국어 설정

- `설정 → 언어` 메뉴에서 `한국어` 또는 `English`를 선택할 수 있습니다.
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
개발 중에는 파일을 수정할 때마다 설치 파일을 재생성하지 않습니다. 사용자가 별도로 요청하거나 최종 배포를 준비할 때 아래 명령으로 `dist\simple-sync-setup-버전.exe`를 생성합니다. 예를 들어 1.6.3은 `simple-sync-setup-1.6.3.exe`입니다. 그 전에는 빌드와 관련 테스트로 검증합니다.

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
dist\simple-sync-setup-1.6.3.exe
```

설치 프로그램은 `.NET Windows Desktop Runtime`이 없는 PC에서도 실행되도록 self-contained publish 결과를 포함합니다.
같은 버전의 설치 파일이 있으면 최신 publish 결과로 덮어씁니다. 다른 버전의 설치 파일은 별도 이름으로 유지합니다.

## GitHub Release 생성

설치 파일을 GitHub Release에 업로드하려면 먼저 설치 프로그램을 생성합니다.

```powershell
.\scripts\publish-installer.ps1
```

Release 생성 전 확인:

```powershell
.\scripts\create-github-release.ps1 -DryRun
```

실제 Release 생성:

```powershell
.\scripts\create-github-release.ps1
```

기본값은 `VERSION` 파일의 버전을 읽어 `v버전` 태그를 만들고, `dist\simple-sync-setup-버전.exe`와 `release-notes-버전.md`를 사용합니다.
예를 들어 `VERSION`이 `1.6.3`이면 `v1.6.3` 태그와 `release-notes-1.6.3.md`를 사용합니다.

다른 저장소, 브랜치, 파일을 지정해야 하는 경우:

```powershell
.\scripts\create-github-release.ps1 `
  -Repository "endy1328/simple-sync" `
  -Target "main" `
  -Version "1.6.3" `
  -InstallerPath ".\dist\simple-sync-setup-1.6.3.exe" `
  -NotesPath ".\release-notes-1.6.3.md"
```

스크립트는 설치 파일의 SHA256도 출력합니다. 이 값은 winget manifest의 `InstallerSha256`에 사용합니다.

## winget 등록 준비

winget manifest는 이 저장소 안에 만들지 않고, `microsoft/winget-pkgs`를 fork한 별도 저장소에서 관리합니다.

권장 위치:

```text
C:\workspace\winget-pkgs
```

기존 공개 버전인 `simple sync` 1.2.1 manifest 위치입니다. 새 버전 등록 시 별도 winget 저장소에서 해당 버전 manifest를 준비합니다.

```text
C:\workspace\winget-pkgs
└─ manifests
   └─ e
      └─ Endy1328
         └─ SimpleSync
            └─ 1.2.1
               ├─ Endy1328.SimpleSync.yaml
               ├─ Endy1328.SimpleSync.installer.yaml
               └─ Endy1328.SimpleSync.locale.en-US.yaml
```

검증:

```powershell
cd C:\workspace\winget-pkgs
winget validate .\manifests\e\Endy1328\SimpleSync\1.2.1
```

로컬 manifest 설치 테스트는 관리자 권한 PowerShell에서 아래 기능을 한 번 켠 뒤 실행합니다.

```powershell
winget settings --enable LocalManifestFiles
winget install --manifest .\manifests\e\Endy1328\SimpleSync\1.2.1
```

공식 winget 등록은 `C:\workspace\winget-pkgs`에서 변경분을 커밋하고 `microsoft/winget-pkgs`로 PR을 제출해야 완료됩니다.

## 설정 파일

`config.toml`은 `simple sync.exe`와 같은 폴더에 저장됩니다. 앱은 시작 시 이 파일을 읽고, 화면에서 설정이 변경되거나 앱이 종료될 때 최신 값을 저장합니다.
설정은 같은 폴더의 고유한 임시 파일에 먼저 기록하고 디스크에 반영한 뒤 교체합니다. 쓰기 또는 교체가 실패하면 기존 설정을 보존하고 오류를 알립니다.

저장소에는 개인 경로가 들어가는 실제 `config.toml` 대신 `config.example.toml` 예제 파일만 포함합니다.

## 설정 예시

```toml
interval_seconds = 10
skin = "syncback_blue"
language = "ko-KR"
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
- 전송 중인 파일 경로와 완료한 바이트 수를 진행 상태로 보고합니다. Drive의 일시적 통신 오류와 호출 제한은 제한된 횟수로 재시도합니다.
- Activity 완료 로그에는 복사, 유지, 삭제, 제외, 실패 개수를 표시합니다.
- 로컬 폴더 간 복사 후 타겟 파일의 마지막 수정 시간을 소스 파일과 맞춥니다.
- `Mirror source` 모드에서도 소스 탐색이나 복사 중 실패가 있으면 안전을 위해 삭제 단계는 건너뜁니다.
- 소스와 타겟이 같거나 어느 한쪽이 다른 쪽 내부인 경우 복사와 삭제를 시작하지 않고 실패로 처리합니다. PowerShell fallback에도 같은 경로 검사를 적용합니다.
- 소스 누락, 빈 경로, 경로 해석 오류, 타겟 생성 오류는 해당 쌍의 실패 건수와 실패 진행 상태로 보고하며, 다음 동기화 쌍은 계속 처리합니다.
- 파일 시스템 오류는 앱 로그에 표시되며, 가능한 경우 다른 파일과 다른 동기화 쌍 처리는 계속합니다.
- 앱 종료 시 자동 실행 타이머를 멈추고 진행 중인 동기화에 취소 신호를 보냅니다.

일반 파일은 내용 해시로 비교합니다. Google 문서는 원본 ID·변경 정보와 마지막 다운로드 파일의 해시로 비교하며, 타겟에서 따로 수정한 파일도 소스 기준으로 복원합니다.

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
- `scripts/create-github-release.ps1`
- `release-notes-*.md`
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
