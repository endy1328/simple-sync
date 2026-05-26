# Harness Guide

This document describes how to build, run, publish, and verify `simple sync`.
When this file changes, update `HARNESS_kor.md` in the same change.

## Environment

- Windows
- .NET SDK with Windows Desktop support
- Target framework: `net10.0-windows`

## Build

```powershell
dotnet build
```

Expected result: build succeeds with zero errors.

## Test

```powershell
dotnet run --project .\tests\SimpleSync.Tests\SimpleSync.Tests.csproj
```

Expected result: all console checks print `PASS`.

## Run

```powershell
dotnet run
```

The app opens a Windows Forms UI titled `simple sync`.
Debug runs use a different single-instance mutex from Release, so an installed Release app and a development `dotnet run` app can run at the same time for comparison.

## Publish

Current release version lives in `VERSION`. `scripts\publish-installer.ps1` reads that value when generating the installer.

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Expected output:

```text
bin\Release\net10.0-windows\win-x64\publish\simple sync.exe
```

## Manual Verification Checklist

- Start the app and confirm existing `config.toml` values load.
- With an installed Release app already open, run `dotnet run` and confirm the Debug app can open side by side.
- Add at least two enabled pairs and confirm both are saved.
- Click `Add Pair` and confirm the new pair starts unchecked in the `On` column.
- Click `Now` and confirm changed files copy from source to target.
- Confirm the `Progress` and `Current` columns update while sync is running.
- Confirm Activity can be filtered by `All`, `Selected`, and `Errors`.
- Confirm unchanged files are skipped on the next run.
- Confirm the default `Copy changes` mode preserves files that exist only in the target.
- Set one pair to `Mirror source` and confirm target-only files are deleted only after source files copy successfully.
- Disable one pair and confirm it is not processed.
- Set a missing source path and confirm the app logs the issue.
- Set a target path inside its source and confirm the app skips it.
- Change the interval and confirm the value is saved to `config.toml`.
- Change a pair mode and confirm `mode = "copy"` or `mode = "mirror"` is saved to `config.toml`.

## Config Location

At runtime, `config.toml` lives beside the executable. In development this is usually:

```text
bin\Debug\net10.0-windows\config.toml
```

In a published build this is usually:

```text
bin\Release\net10.0-windows\win-x64\publish\config.toml
```
