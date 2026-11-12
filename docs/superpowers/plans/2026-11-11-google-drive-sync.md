# Google Drive 1.3.0 Implementation Plan

> **For agentic workers:** Execute with subagent-driven-development, using separate ownership for authentication, Drive transport, and shared-engine/UI integration. Do not commit or change the existing user .gitignore edit. Repository metadata is read-only in this environment; implement in the provided workspace.

**Goal:** Deliver multi-account personal My Drive upload/download, copy/mirror with permanent deletion, and Office exports in simple sync 1.3.0.

**Architecture:** Add storage endpoints consumed by a shared planner/executor. Preserve the established local engine while both storage implementations share the new cross-storage engine. Store encrypted OAuth credentials separately from TOML and persist successful source/target identities in an atomic local state file.

**Tech Stack:** .NET 10 WinForms, HttpClient/Drive v3, Windows DPAPI, OAuth authorization code with PKCE, JSON state storage.

## Tasks

- [x] 1. Baseline: run `dotnet run --project tests/SimpleSync.Tests/SimpleSync.Tests.csproj` with TEMP/TMP inside `artifacts/test-temp`. Preserve unrelated edits.
- [x] 2. Contracts/config: add `Storage/StorageContracts.cs`; extend `SyncPair` with SourceType, TargetType, DriveAccountId; round-trip optional TOML keys, treating omitted types as local. Add failing config tests first.
- [x] 3. Authentication: `GoogleDrive/GoogleAccountService.cs` implements client JSON import, multi-account browser login, state/PKCE validation, bounded callback, refresh, encrypted token storage, account removal. Tests use temporary files and injected HTTP transport; never contact real accounts automatically.
- [x] 4. Drive endpoint: `GoogleDrive/GoogleDriveEndpoint.cs` implements complete paginated folder traversal, ID-based matching, native document export naming, streaming downloads, resumable uploads, ID reservation before creates, bounded transient retries, file-level permanent deletes and empty-folder verification. Folder picker consumes direct child listing. Fake HTTP tests assert pagination, exports, errors and deletion semantics.
- [x] 5. Common engine: `Storage/EndpointSyncService.cs` and local endpoint compare by hash, detect ambiguous paths and type conflicts before mutation, transfer source-first, preserve target on failure, suppress mirror deletes on all listing/transfer failures, preserve filter exclusions, and persist successful native exports. Exercise with real temporary directories and a fake remote endpoint, including newer targets, empty sources, interrupted listing, collisions, and deletion exclusions.
- [x] 6. UI: add account management, Drive folder browsing and pair connection dialogs; integrate through MainForm, preserve ko-KR/en-US, disable editing while busy, allow cancellation. Validate supported directions and account selection before enabling a pair.
- [x] 7. Integration/review: run full console tests and Release build; inspect requirements and error paths, fix discovered defects with regressions. No real OAuth sign-in or account mutations without user interaction.
- [x] 8. Release: update VERSION, csproj version fields, installer default version, README, CHANGELOG and release-notes-1.3.0.md. Rebuild installer using scripts/publish-installer.ps1 after verifying staging deletion target remains inside artifacts/installer. Inspect generated app/setup version metadata.

## Acceptance commands

```powershell
New-Item -ItemType Directory -Force artifacts/test-temp | Out-Null
$env:TEMP = (Resolve-Path artifacts/test-temp).Path
$env:TMP = $env:TEMP
dotnet run --project tests/SimpleSync.Tests/SimpleSync.Tests.csproj
dotnet build -c Release
.\scripts\publish-installer.ps1
git diff --check
```

Expected: console tests exit 0, Release build has no errors, installer contains app 1.3.0. Live cloud verification remains explicitly distinguished from simulated HTTP tests until a real OAuth client and interactive account sign-in are supplied.

## Implementation record

- Tasks 1–8 implemented in the provided workspace. No Git metadata or unrelated .gitignore edits changed.
- Source, target, and account isolation tests run with local temporary directories and simulated HTTP only; no live Google account was connected.
- Independent review found and corrected extended-path overlap bypass, busy-state custom paste/filter editing, hidden failure diagnostics, stale cancellation progress and initialization-time settings writes.
- Before mirror deletion, the engine re-inventories the source and aborts deletion if source identities or versions changed during transfer.
- UI rendering and direction/draft/cancellation behavior were checked with an STA harness; images are under ignored `artifacts/drive-dialog-check`.
- Final command results and artifact versions are recorded in `docs/superpowers/plans/2026-11-11-google-drive-sync-validation.md` after verification.
