@echo off
setlocal EnableExtensions DisableDelayedExpansion

REM Emergency fallback example for environments where custom EXE files are blocked.
REM Recommended usage for paths with spaces or Korean text:
REM   scripts\simple-sync-robocopy.cmd "\\server\share\source" "C:\target folder"
REM
REM Robocopy copies only new or changed files by default.

chcp 65001 >nul

set "SOURCE=%~1"
set "TARGET=%~2"

REM If no arguments were passed, edit the fallback values below.
if not defined SOURCE set "SOURCE=C:\source"
if not defined TARGET set "TARGET=D:\backup"

if not defined SOURCE (
  echo SOURCE is empty.
  exit /b 1
)

if not defined TARGET (
  echo TARGET is empty.
  exit /b 1
)

echo simple sync robocopy fallback
echo "%SOURCE%" --^> "%TARGET%"

robocopy "%SOURCE%" "%TARGET%" /E /COPY:DAT /DCOPY:T /R:1 /W:1
set "RC=%ERRORLEVEL%"

if %RC% LEQ 7 (
  echo Done. robocopy exit code %RC%
  exit /b 0
)

echo Failed. robocopy exit code %RC%
exit /b %RC%
