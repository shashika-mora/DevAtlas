# DevAtlas roadmap

## Product direction

DevAtlas is a local-first desktop workspace manager for developers. The product should remain useful without an account, an API key, or an internet connection. Network access belongs only to operations that genuinely need it, such as Git fetch, pull, push, clone, and application updates.

The roadmap is deliberately organized around usable milestones. Each milestone should leave the application in a buildable state and should be delivered as a small vertical slice rather than as a collection of placeholder services.

## Current status

Version 0.1.0 delivers the discovery preview:

- .NET 10 solution with Domain, Application, Infrastructure, App, and Tests projects.
- Avalonia desktop shell.
- SQLite storage for workspace roots and discovered projects.
- Asynchronous filesystem discovery with common directory exclusions.
- Marker-based technology detection including .NET and ordinary Git repositories.
- Local Git branch, status, changed-file, and recent-commit inspection.
- Virtualized dashboard, search, favorites, root controls, Git details, progress, and cancellation.
- Stable identity, metadata-preserving reconciliation, schema migration, regression tests, local diagnostics, and portable x64 packaging.

Restore/build/test and release evidence are recorded in `RELEASE-0.1.0.md`. Continue treating preview validation separately from production readiness.

## Milestones

### 0.1 â€” Discover

- Stabilize the build, test, logging, and migration setup.
- Finish workspace-root management, including remove, ignore, and rescan actions.
- Make project identity stable across rescans.
- Harden discovery around permissions, symlinks, reparse points, nested repositories, and monorepos.
- Add a project details page.
- Complete the read-only Git snapshot.
- Add CI and release-quality documentation.

### 0.2 â€” Understand

- Detect installed developer tools and project runtime requirements.
- Add project filters, sorting, favorites, tags, and archive state.
- Add recent-project and DevAtlas-owned activity history.
- Show project warnings without inventing an arbitrary health score.
- Add notes and per-project preferences.

### 0.3 â€” Run

- Persist detected and user-defined Run, Build, and Test commands.
- Use structured executable/argument models instead of unsafe shell concatenation.
- Require explicit user action before every command.
- Stream output, capture exit codes, record duration, and support cancellation.
- Track processes launched by DevAtlas.
- Add a basic listening-port viewer.

### 0.4 â€” Resume

- Add workspace sessions and a recovery view.
- Show the last branch, Git changes, commands, runtime, ports, and DevAtlas-launched processes.
- Add Open Folder, Open Terminal, and configured IDE actions.
- Add Docker and Docker Compose detection and read-only visibility.
- Make resuming explicit and user-controlled; do not restore arbitrary applications automatically.

### 0.5 â€” Control

- Add safe Git mutations with confirmation: stage, unstage, commit, branch, checkout, and stash.
- Add Docker Compose start, stop, restart, and logs.
- Add richer process controls and environment matching.
- Keep online Git operations visibly separate from offline functionality.

### 1.0 â€” Ship

- Windows installer and uninstall support.
- Version metadata, application icon, About page, changelog, and release notes.
- Database migrations and upgrade tests.
- Accessibility and keyboard-navigation pass.
- Performance validation against large workspaces.
- Reproducible CI artifacts, checksums, and dependency-license inventory.

## Engineering standards for every milestone

Every feature should include its domain model only when persistence is justified, an application boundary, infrastructure behavior, UI loading/error states, cancellation for long-running work, tests, logging, offline behavior, and documentation. Repository content is untrusted: DevAtlas must never execute detected scripts automatically.

Performance work should focus on bounded concurrency, incremental results, virtualized lists, stable project identities, scan fingerprints, database indexes, and avoiding repeated Git/process work. Platform-specific behavior belongs behind interfaces so Linux and macOS support remain possible.
