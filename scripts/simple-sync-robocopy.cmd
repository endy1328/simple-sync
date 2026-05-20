@echo off
setlocal

REM Emergency fallback example for environments where custom EXE files are blocked.
REM Edit SOURCE and TARGET below, then run this file.
REM Robocopy copies only new or changed files by default.

set "SOURCE=C:\source"
set "TARGET=D:\backup"

if "%SOURCE%"=="" (
  echo SOURCE is empty.
  exit /b 1
)

if "%TARGET%"=="" (
  echo TARGET is empty.
  exit /b 1
)

echo simple sync robocopy fallback
echo %SOURCE% --^> %TARGET%

robocopy "%SOURCE%" "%TARGET%" /E /COPY:DAT /DCOPY:T /R:1 /W:1
set "RC=%ERRORLEVEL%"

if %RC% LEQ 7 (
  echo Done. robocopy exit code %RC%
  exit /b 0
)

echo Failed. robocopy exit code %RC%
exit /b %RC%
