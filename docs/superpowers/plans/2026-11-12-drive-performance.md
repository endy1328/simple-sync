# Drive small-file performance implementation plan

**Goal:** Reduce repeated Drive requests and overlap independent small-file transfers for a 454-folder/2070-file workload without weakening mirror deletion and source-first guarantees.

**Architecture:** Scope metadata caches to one endpoint/run. Prepare directory structure before transfers, then allow at most four small binary transfers when the Drive endpoint advertises concurrency support. Keep local-only, native exports, and large transfers sequential. Preserve full source inventory, pre-delete source rescan, checksum checks, fresh deletion membership/empty-folder checks and cancellation barriers.

**Execution:** Use subagent-driven-development with separate ownership of transport and common engine; review both before integration. No installer generation or live account mutations. Git metadata is read-only; leave user edits untouched.

- [x] Transport baseline: add request counters and replayable HTTP performance fixture; capture request counts before changes.
- [x] Transport optimization: simplify repeated directory preparation, batch reserved IDs, multipart uploads for files <=1 MiB, verification using returned metadata with fresh fallback, thread-safe caches. Preserve fresh mutation-boundary child/name, root/ancestry checks and fresh pre-delete checks. Never use cached child-list absence to authorize creation.
- [x] Engine optimization: add explicit endpoint capability `IConcurrentTransferEndpoint.MaxConcurrentTransfers`; skip directory preparation when target inventory already contains that directory; bounded parallel work only for small hashed files when a Drive endpoint opts in; synchronize counts/state/progress and await in-flight work before mirror deletion or cancellation completion.
- [x] Regression tests: bounded concurrency, source-first contents, failure/cancellation suppression of delete, native export state, changed sources, duplicate names, moved roots/ancestors, unplanned targets, lost responses and large-file resumable uploads.
- [x] Benchmark: synthetic 454-folder/2070-small-file initial upload and unchanged repeat; record request counts (including operation breakdown), not a fabricated real-cloud speedup.
- [x] Integrate review and complete all console tests and Release build. Update PATCH version 1.5.1, changelog, README and release notes. Do not build an installer.

Commands:
```powershell
dotnet run -c Release --project tests/SimpleSync.Tests/SimpleSync.Tests.csproj
dotnet build -c Release --no-restore
git diff --check
```

Official protocol references: [uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads), [reserved IDs](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/generateIds).

Review decision: per-parent write child-list caching was tested and removed because an external file/folder created after the first cached lookup could be missed. Prepared directory IDs may be reused, but fresh child-list conflict checks remain before each actual write/create. This favors preserving existing collision behavior over a larger request-count reduction.
