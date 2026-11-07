# Version 0.1.0 release validation

Date: 2026-11-07. Distribution: Windows x64 portable preview, self-contained .NET runtime, unsigned.

## Scope

This release stabilizes discovery and adds root controls, favorites, a virtualized project list, Git details, cancellation, migration, and local diagnostics. It does not implement project command execution, processes, ports, runtime management, or session restoration.

## Validation record

Local environment: Windows x64, .NET SDK 10.0.402. Regression tests cover 35 cases including actual Git processes, linked worktrees, custom helpers, inherited environment overrides, directory links, legacy SQLite upgrade, incomplete scans, stable identity, favorites, and view-model commands.

Commands used:

```powershell
dotnet restore DevAtlas.sln
dotnet format DevAtlas.sln --verify-no-changes --no-restore
dotnet build DevAtlas.sln --configuration Release --no-restore
dotnet test DevAtlas.sln --configuration Release --logger trx
dotnet list DevAtlas.sln package --vulnerable --include-transitive --no-restore --format json
./eng/Publish-Release.ps1
git diff --check
```

Local restore, Release build, all 35 tests, formatting verification, whitespace checks, and the dependency advisory query passed. The query returned no known advisory matches. The initial portable package launched successfully; all 366 initial packaged payload hashes matched. Desktop checks verified folder selection, three technology types, Git details, favorites across restart, rescan selection, and filtering. Final packages are rebuilt from the clean release commit and record that revision in `BUILD-INFO.json`.

Final artifact verification and GitHub CI evidence are recorded in the release notes and the dated workspace handoff. Do not infer platform execution from an included native dependency: the portable artifact targets Windows x64 only. Ubuntu CI validates source build and automated tests; it is not Linux desktop acceptance testing.

## Desktop acceptance

Use `DEVATLAS_DATA_DIRECTORY` to isolate test data. Exercise the folder picker, scan completion and warnings, project selection/details, favorite persistence, rescan, search, and restart. Root remove/ignore commands are also covered programmatically to verify that workspace files remain untouched. Do not use the real user profile for automated acceptance tests.

## Packaging and integrity

The publish script refuses to overwrite an existing artifact directory. It packages the entire publish directory, upstream notices, resolved dependency metadata, and `FILE-HASHES.json`. `SHA256SUMS.txt` covers the ZIP and standalone dependency inventory. After upload, download release assets and compare their SHA-256 values to the local artifacts.

## Remaining limits

- Unsigned early preview; no installer, automatic updates, or ARM64/macOS desktop validation.
- No performance claim for very large workspaces; Git output is bounded and can become a warning.
- Git subprocess reads are not an atomic snapshot and are not an OS sandbox.
- Directory reparse checks cannot eliminate all races during concurrent hostile filesystem changes.
- Keep a closed-app backup of the complete data directory before upgrading existing installations.
