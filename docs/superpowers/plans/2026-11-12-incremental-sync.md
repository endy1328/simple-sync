# Incremental repeat synchronization implementation plan

**Goal:** Reduce repeated Drive directory listing and local content hashing when only a few daily files change, while preserving source priority and mirror deletion safeguards.

**Architecture:** Persist per-account/root/filter Drive directory snapshots plus a Changes API cursor. Capture a cursor before a full scan; consume changes before using saved snapshots and refresh only affected known parent folders. Folder structure changes, invalid cursors, stale/corrupt caches, malformed/incomplete feeds or incompatible scope fall back to a full scan (ordinary auth/network failures still fail the run). Pin full-scan age to the original scan time so incremental activity cannot postpone recovery indefinitely. Preserve all fresh write/read/delete identity checks. Local caching uses a strong NTFS file identity/change marker, with a bounded-age full verification and fallback to hashing when metadata cannot be trusted. Mirror source revalidation always bypasses caches.

**Tech stack:** .NET 10 Windows, System.Text.Json, Drive v3 REST, existing console regression harness. No installer or Git writes.

- [x] Add failing Drive cursor/snapshot tests, then implement an opt-in persistent inventory cache in a partial GoogleDriveEndpoint implementation. Default test/picker endpoints remain uncached; production passes account identity.
- [x] Implement and test local hash reuse with reliable NTFS change identity. Retain safe fallback and a fresh-list API for mirror source verification.
- [x] Integrate endpoint construction and mirror fresh inventory. Verify unchanged local sources repair changed/deleted remote targets. Skip the second full scan when there are no mirror deletion candidates.
- [x] Test cache isolation, expired/malformed feeds, cache corruption, changed filters, file changes/deletion/move, folder rename/move and excluded paths, and failures/cancellation without unsafe cache promotion. Complete independent specification and code-quality reviews and resolve findings.
- [x] Measure warm unchanged runs and a small changed-file set using the HTTP fake; run all 41 regression groups and Release build; update PATCH 1.6.3 metadata/changelog/release notes and record limitations. No installer or Git writes.
