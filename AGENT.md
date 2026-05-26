# Agent Operating Guide

This document is the English source of truth for agents working on `simple sync`.
When this file changes, update `AGENT_kor.md` in the same change.

## Project Goal

Build and maintain a simple Windows application named `simple sync` that synchronizes files from configured source directories to target directories.

## Working Rules

- Keep the app simple and local-first.
- Preserve one-way copy behavior from source to target.
- Pair mode defaults to `copy`, which copies new or changed files and preserves target-only files.
- `mirror` mode is allowed only as an explicit per-pair option and deletes target-only files after a successful copy pass.
- Do not add two-way sync unless explicitly requested.
- Keep large-file copies safe by copying to a temporary file first and replacing the target only after copy success.
- Treat sync filters as the pair's managed universe. Files outside the filter must not be copied or deleted, including in mirror mode.
- Keep runtime sync progress separate from persisted `SyncPair` configuration.
- Keep settings in `config.toml` beside the executable.
- Keep Debug builds on a separate single-instance mutex from Release so installed builds and `dotnet run` can be compared side by side during development.
- Keep release version in `VERSION`, update `CHANGELOG.md`, and keep project/installer versions aligned.
- Keep README and harness documents current after implementation changes.
- Avoid overwriting user changes that are unrelated to the current task.
- Use focused changes and verify with `dotnet build` and the console sync tests before handoff.

## Safety Rules

- Reject or skip a target path that is the same as the source or inside the source.
- Treat missing sources, locked files, and permission errors as reportable sync failures.
- Continue processing other files and pairs after recoverable file system errors.
- Retry transient file copy failures briefly before recording a final failure.
- Keep destructive file operations limited to explicit `mirror` mode.
- Skip mirror deletion if source traversal or copy fails for that pair.

## Documentation Rule

Operational and harness documents are maintained in English and Korean pairs:

- `AGENT.md` and `AGENT_kor.md`
- `HARNESS.md` and `HARNESS_kor.md`

Update both files together whenever operational behavior, verification, or project workflow changes.
