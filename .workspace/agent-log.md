# Agent log

## 2026-09-24

- PR #5 was merged into `main`.
- Dependency maintenance PR #6 remains open. Its root cause is the vulnerable transitive dependency graph: `SQLitePCLRaw.lib.e_sqlite3` comes through `Microsoft.Data.Sqlite`, and `Tmds.DBus.Protocol` comes through the Avalonia desktop dependency graph. The proposed maintenance change uses central transitive pinning rather than warning suppression or CI weakening.
- PR #6 validation is blocked in this environment because `global.json` requires .NET SDK `10.0.100`, which is not installed. Restore, vulnerability listing, build, and test checks therefore could not run; `git diff --check` passed.


## 2026-11-07 — discovery preview 0.1.0

- Reconciled the prior handoff: PR #6 is merged; the old SDK blocker no longer applies. SDK 10.0.402 is installed and accepted by `global.json`. Incorporated the newer remote README commit before implementation.
- Branch: `codex/discovery-release-v0.1.0`. Implemented application scan coordination, .NET/Git detection, stable identity, metadata-preserving reconciliation, legacy schema migration, incomplete-root retention, cancellation, bounded Git processes, and restricted Git helper/environment behavior.
- Added root remove/ignore controls, favorites, virtualized project browsing, Git details, persistent status/warnings, local diagnostics, and isolated data profiles. Corrected refresh selection, line-ending policy, generated-file exclusions, attribution guidance, and NuGet maintenance.
- `dotnet test DevAtlas.sln --configuration Release --logger trx`: passed, 35 tests, 0 failed, 0 skipped. Solution/application compilation passed with 0 warnings and 0 errors.
- `dotnet format DevAtlas.sln --verify-no-changes --no-restore --verbosity quiet` and `git diff --check`: passed. `dotnet list DevAtlas.sln package --vulnerable --include-transitive --no-restore --format json`: no known advisory matches. Targeted credential-pattern scan found no matches in project sources.
- `./eng/Publish-Release.ps1`: initial Windows x64 self-contained package passed; 34 package/runtime components inventoried and 366 packaged file hashes verified. The final package is built again from the clean release commit and records its commit/SDK in `BUILD-INFO.json`.
- Desktop acceptance used only an isolated profile and test-owned workspace: folder picker, .NET/Rust/Node discovery, branch/changes, details, favorites, restart, rescan selection, favorites filtering, and empty search results verified. Programmatic tests verify that root controls leave workspace files unchanged. The real user application-data database was not used.
- Release scope: unsigned Windows x64 preview. CI now covers Windows and Ubuntu source builds/tests and Windows packaging. Runtime/process/port management and session restoration remain roadmap work. GitHub release notes carry final CI and uploaded-asset verification evidence.
