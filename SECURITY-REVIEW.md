# Security review

**Review date:** 2026-09-22  
**Scope:** DevAtlas source, project files, CI configuration, dependency declarations, and local process/filesystem integrations.

## Executive summary

The review found two local attack paths. Both have been fixed:

1. Git metadata inspection could honor repository-controlled helpers when reading an attacker-controlled repository.
2. Discovery could follow symbolic links, junctions, or other reparse points outside a selected workspace root.

No hardcoded credentials, obvious secret leakage, authentication or authorization subsystem, network service, or known dependency vulnerability was found in the reviewed code. Dependency advisories still need to run in CI once the .NET SDK is available.

## Findings

| # | Severity | Location | Finding | Status |
|---|---|---|---|---|
| 1 | High | `src/DevAtlas.Infrastructure/GitReader.cs` | Git was launched against user-selected repositories without fully isolating repository-controlled helpers. A malicious repository could use Git configuration such as `core.fsmonitor`, external diff, or filters to execute code during an automatic scan. | Fixed |
| 2 | Medium | `src/DevAtlas.Infrastructure/ProjectDiscovery.cs` | Directory traversal did not reject reparse points. A junction or symlink inside a selected root could cause discovery and Git inspection outside the approved boundary. | Fixed |

## Remediation details

### Git helper execution

`ProcessGitReader` now:

- passes arguments through `ProcessStartInfo.ArgumentList`;
- disables system and global Git configuration;
- provides an empty temporary global configuration;
- disables hooks, filesystem monitors, external diff, and LFS filter processes;
- disables interactive prompts and optional locks;
- reports non-zero Git exit codes and stderr instead of treating failures as empty metadata;
- removes temporary configuration files after each invocation.

The application still runs Git as the current user. This is not an OS sandbox; future releases should consider a stronger isolation boundary for processing repositories that are not trusted by the user.

### Filesystem containment

The discovery engine now treats reparse points as scan boundaries and skips them before adding child directories to the traversal queue. This prevents the ordinary discovery walk from following Windows junctions or symbolic links outside the selected root.

## Other bugs found

These are reliability issues rather than confirmed vulnerabilities:

- Startup initialization previously ran without an error-handling boundary. Initialization failures are now surfaced in the status message.
- Concurrent rescans could interleave. A semaphore now serializes scans.
- Git failures were previously converted to empty output. Failures now remain visible to the UI.
- Equivalent root paths can still be improved with canonical identity checks for relative-path and alias cases.

## Dependency and legal review

The repository declares Avalonia, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, Microsoft.Extensions packages, and xUnit packages. No dependency was identified as obviously unsafe from the manifests alone. A release still requires:

- `dotnet list package --vulnerable`;
- lockfile/assets review;
- dependency license inventory;
- GitHub Actions and Dependabot review;
- secret scanning;
- static analysis;
- regression tests for Git process isolation and path containment.

The repository is licensed under MIT. `LEGAL.md` records the requirement to ship applicable third-party notices and to review platform redistribution and privacy obligations before distributing installers.

## Validation limits

`git diff --check` passed. Full restore, build, and test execution could not be performed in this environment because the .NET SDK is unavailable. The fixes therefore require CI validation before release.
