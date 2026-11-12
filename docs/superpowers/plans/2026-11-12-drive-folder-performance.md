# Drive first-upload folder preparation performance

The first-upload flow prepares every missing folder serially before uploading files. The Drive endpoint also holds one global folder lock during network requests. Small-file uploads already run with four workers, so folder preparation remains a separate source of network latency.

Plan: retain all fresh ancestry/name checks and prepare independent folders with up to four workers. Add a separate opt-in directory concurrency capability so existing endpoints keep their serial contract. Process one path-depth level at a time; complete all folder tasks before file transfers, and join/cancel active tasks on failure. Drive uses a case-insensitive per-path lock to prevent duplicate folder creation, with a short root initialization lock. Account/network policy is unchanged.

1. Add deterministic tests proving concurrent folder creation, shared-parent deduplication, depth barriers, worker limits and failure/cancellation barriers. Observe the serial implementation failing the new concurrency tests.
2. Add the opt-in interface in StorageContracts.cs, level-based preparation in EndpointSyncService.cs and path locks in GoogleDriveEndpoint.cs.
3. Run regression tests and compare serial versus concurrent preparation with identical synthetic latency. Report synthetic results separately from real Drive timing.
4. Update PATCH version to 1.6.2, changelog, current README/version metadata and release notes. Do not generate an installer.
