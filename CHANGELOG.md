# Changelog

All notable changes to DevAtlas will be documented here.

## 0.1.0 — 2026-11-07 (preview)

### Added

- Workspace-root add, remove, ignore, and rescan controls.
- Virtualized project list with search, favorites, and Git details.
- Background discovery with progress, cancellation, stable path identities, and .NET detection.
- Application-level scan coordination with bounded Git concurrency and transactional reconciliation.
- Versioned SQLite migration, conservative handling of unavailable roots, retained metadata for returning projects, and local rotating diagnostics.
- Portable self-contained Windows x64 packaging, dependency inventory, upstream notices, and SHA-256 checksums.

### Fixed

- Test compilation and missing Git-directory detection.
- Invalid Git status option placement, empty repositories, detached HEAD, rename and Unicode filename parsing.
- Project identity/favorite loss, duplicate cards from overlapping roots, stale projects returning after restart, and scan timestamps overwriting activity.
- Hidden status messages, incomplete error boundaries, UI-thread database/filesystem work, and project selection after refresh.
- Git helper isolation, inherited Git environment overrides, sequential output draining, missing timeouts, and process cleanup on cancellation.
- Generated-file exclusions, line-ending policy, missing NuGet maintenance, and stale workspace/review documentation.

### Limitations

- Windows x64 preview only; unsigned portable distribution with no installer or updater.
- Processes, ports, runtime management, command execution, and session recovery remain future milestones.
- Technology classification is marker-based; Git metadata reads are separate operations rather than an atomic repository snapshot.
- Directory link checks are not a security sandbox against concurrent hostile filesystem changes.
