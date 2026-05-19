# Agent Operating Guide

This document is the English source of truth for agents working on `simple sync`.
When this file changes, update `AGENT_kor.md` in the same change.

## Project Goal

Build and maintain a simple Windows application named `simple sync` that copies changed files from configured source directories to target directories.

## Working Rules

- Keep the app simple and local-first.
- Preserve one-way copy behavior from source to target.
- Do not add target deletion or two-way sync unless explicitly requested.
- Keep settings in `config.toml` beside the executable.
- Keep README and harness documents current after implementation changes.
- Avoid overwriting user changes that are unrelated to the current task.
- Use focused changes and verify with `dotnet build` before handoff.

## Safety Rules

- Reject or skip a target path that is the same as the source or inside the source.
- Treat missing sources, locked files, and permission errors as reportable sync failures.
- Continue processing other files and pairs after recoverable file system errors.
- Do not make destructive file operations part of sync behavior.

## Documentation Rule

Operational and harness documents are maintained in English and Korean pairs:

- `AGENT.md` and `AGENT_kor.md`
- `HARNESS.md` and `HARNESS_kor.md`

Update both files together whenever operational behavior, verification, or project workflow changes.
