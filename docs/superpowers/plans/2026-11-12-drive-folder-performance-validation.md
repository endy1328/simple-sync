# simple sync 1.6.2 folder preparation validation

Date: 2026-11-12

## Scope and measurement

First uploads prepared all missing Drive folders sequentially, before starting the existing four-worker small-file upload stage. The Drive endpoint held a single folder lock throughout metadata checks and creation requests. This change prepares up to four independent folders concurrently, with a barrier between path-depth levels. Per-path locks deduplicate shared parent/child creation. Fresh root/ancestor/name conflict checks remain in place.

The benchmark used the real EndpointSyncService and GoogleDriveEndpoint against the existing synthetic Drive HTTP handler. It generated 454 direct child folders, no files, and inserted a 10 ms delay before every HTTP request. The serial comparison masks the endpoint's directory concurrency capability; all other implementation and test data are identical. Windows timer scheduling means the observed per-request delay may exceed 10 ms. No real account data was read or changed.

| Folder preparation | Elapsed | HTTP requests | Peak concurrent requests |
|---|---:|---:|---:|
| Serial | 21.487 s | 1,369 | 1 |
| Four workers | 5.482 s | 1,369 | 4 |

The synthetic folder preparation stage took 74.5% less time. This does **not** measure full synchronization or actual Google Drive upload time. Request counts, per-folder validation and file-upload behavior are unchanged. Deep chains still depend on their parents, and previously existing folders do not benefit from creation parallelism.

## Validation

- A deterministic HTTP barrier test failed on the prior global-lock implementation and passed with independent concurrent creation.
- Concurrent calls sharing a parent/child path create each folder once.
- Engine tests cover worker limits of one/two/four, clamping a higher capability to four, and serial behavior for endpoints without directory concurrency capability.
- All parent-level folders finish before the child level; all folder workers finish before file upload.
- On failure/cancellation, in-flight workers are joined and uploads/mirror deletion do not run.
- Existing root/ancestor mutation, late-name conflicts, lost responses, hash verification and upload cancellation regressions remain covered.

The local comparison harness is in ignored `artifacts/folder-perf`. Run the regression suite using `dotnet run --project tests/SimpleSync.Tests/SimpleSync.Tests.csproj -c Release`. No installer was generated.
