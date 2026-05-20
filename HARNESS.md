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

## Run

```powershell
dotnet run
```

The app opens a Windows Forms UI titled `simple sync`.

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Expected output:

```text
bin\Release\net10.0-windows\win-x64\publish\simple sync.exe
```

## Manual Verification Checklist

- Start the app and confirm existing `config.toml` values load.
- Add at least two enabled pairs and confirm both are saved.
- Click `Now` and confirm changed files copy from source to target.
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
