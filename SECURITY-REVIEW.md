# Security and reliability review

Review date: 2026-11-07. Scope: the 0.1.0 discovery preview, process/filesystem boundaries, persistence, tests, and release packaging.

## Implemented controls

- Git uses an absolute executable resolved from absolute PATH entries, structured arguments, a restricted environment, empty global configuration, disabled system configuration, and disabled prompts/optional locks/lazy network fetches.
- Inspection overrides hooks, fsmonitor, external diff, signature verification, automatic maintenance, submodule traversal, and every configured clean/smudge/process filter name, including custom filter names.
- Both redirected streams are drained concurrently with bounded output. Each invocation has a deadline; cancellation terminates the owned process tree and waits for exit.
- Discovery runs off the UI thread, checks directory reparse points on visitation, excludes tool/dependency folders, and contains enumeration error handling.
- SQLite uses parameterized commands and a connection-string builder. Operations run on background workers with serialized access and per-operation connections. Schema upgrade and scan reconciliation are transactional.
- Incomplete/cancelled scans do not discard existing metadata. Missing projects are hidden only after complete coverage; identity and favorites survive rediscovery.
- Local diagnostics record error types rather than raw Git output, environment values, exception messages, or source contents. Logs rotate and remain local.

## Verification

Regression tests cover configured Git helpers, inherited Git directory redirection, linked worktrees, empty repositories, detached HEAD, rename/Unicode parsing, cancellation/deadlines, directory-link boundaries, exclusions, identity, favorites, missing and returning projects, unavailable roots, schema upgrade, and view-model state.

Exact release commands and results are recorded in `docs/RELEASE-0.1.0.md`. Dependency advisories are checked against the resolved direct/transitive graph. This is engineering evidence, not a security certification.

## Remaining boundaries

Git still runs as the current user; it is not an OS sandbox. Filesystem checks cannot prevent all races when another process deliberately replaces directories during inspection. Select local repositories you trust enough to inspect. Git snapshots can change between subprocesses. Output above the inspection cap becomes a visible warning rather than an unbounded memory allocation.

The Windows portable preview is unsigned. A production release still needs signing, broader accessibility/performance evaluation, platform testing, and ongoing dependency/license review. No telemetry, account subsystem, remote service, or project-command execution is implemented.
