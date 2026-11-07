# DevAtlas

Offline-first developer workspace manager for discovering local projects, tracking Git state, managing runtimes and ports, running workflows, and resuming development from one desktop app.

DevAtlas is designed to remain useful without an account, an API key, a mandatory cloud service, or generative AI. Repository contents and DevAtlas-owned metadata stay on the local machine. Network access is reserved for explicitly requested operations such as Git fetch, pull, push, clone, and application updates.

## Version 0.1.0 preview

The repository contains a .NET 10 solution split into domain, application, infrastructure, desktop UI, and test projects. The initial slice provides:

- Avalonia dashboard with a virtualized project list, persistent status messages, and Git details
- versioned SQLite persistence with stable project identity and retained favorites
- background discovery, progress, cancellation, and exclusions for dependency, build, Git, and virtual-environment folders
- project technology detection for .NET, Node.js, Java/Kotlin, Rust, Python, Go, PHP, and Docker signals
- local Git branch, status, changed-file, and recent-commit inspection
- project search, favorites, changed-file indicators, workspace rescanning, and root remove/ignore controls
- conservative handling of inaccessible roots and recoverable metadata for projects that disappear and return

The app stores its database under the platform local application-data directory in `DevAtlas/devatlas.db` (normally `%LOCALAPPDATA%\DevAtlas` on Windows). It does not upload repository contents or execute detected commands automatically. Removing or ignoring a root changes DevAtlas metadata only. Projects missing from a completed scan are hidden; saved favorites and identity are retained if they return. Incomplete scans retain existing projects and show warnings.

## Download and run

Get the Windows x64 portable ZIP from [GitHub Releases](https://github.com/shashika-mora/DevAtlas/releases). Extract the **entire folder** and run `DevAtlas.App.exe`. The package includes the .NET runtime; Git must be installed separately for repository details. This first preview is unsigned and has no installer or automatic updater.

Compare the ZIP's SHA-256 with `SHA256SUMS.txt` on the release:

```powershell
Get-FileHash .\DevAtlas-0.1.0-win-x64.zip -Algorithm SHA256
```

Processes, ports, runtime management, command execution, and session restoration remain roadmap features. The preview supports read-only discovery and Git inspection.

## Local data and recovery

Close DevAtlas before copying its entire data directory for a backup. The database uses SQLite WAL; copy the whole directory together rather than copying an open database alone. Version 0.1.0 upgrades the original schema transactionally and preserves existing project IDs and favorites. Databases from a newer schema are rejected without schema changes.

For a separate data profile, set `DEVATLAS_DATA_DIRECTORY` to an absolute folder path before launch. Local diagnostics rotate after 1 MiB and record operation/error types without Git output, exception messages, environment variables, or repository contents. No logs are uploaded.

## Prerequisites

- .NET SDK 10.0.100 or a compatible .NET 10 SDK.
- Git available on `PATH` for repository inspection.
- Windows is the first supported desktop platform. The core projects avoid platform-specific assumptions where practical.
- Internet access is required for the first package restore unless all NuGet packages are already cached locally. The application itself does not require internet access after installation.

The required SDK version is declared in `global.json`. Check the installed SDKs with:

```powershell
dotnet --list-sdks
dotnet --info
git --version
```

## Build, run, and test

Run these commands from the repository root:

```powershell
dotnet restore
dotnet build DevAtlas.sln --configuration Release
dotnet test DevAtlas.sln --configuration Release --no-build
```

To build and run the desktop application:

```powershell
dotnet run --project src/DevAtlas.App --configuration Release
```

For iterative development, omit `--configuration Release`. The application starts with no configured roots; choose **Add workspace root** to select a directory and begin discovery.

Useful validation commands:

```powershell
dotnet format DevAtlas.sln --verify-no-changes
dotnet list DevAtlas.sln package --vulnerable
git diff --check
```

`dotnet list ... package --vulnerable` requires access to the configured NuGet audit source. A vulnerability result must be investigated and fixed or explicitly documented; do not suppress it or weaken `TreatWarningsAsErrors`.

### Troubleshooting

If `dotnet` is not recognized, install the .NET 10 SDK and restart the terminal. If the SDK selected by `global.json` is not installed, install that SDK or use a compatible .NET 10 SDK that satisfies the file's roll-forward policy.

If restore reports a package vulnerability, do not bypass the warning. Review the dependency graph, update the affected direct or transitive package, and rerun restore, vulnerability checks, build, and tests.

If Git is unavailable, the application can still start, discover projects, and show local metadata that does not require Git. Git-specific information should be presented as unavailable rather than treated as clean.

## Project layout

```text
src/
  DevAtlas.Domain/          Domain models and value objects
  DevAtlas.Application/     Application boundaries and use-case abstractions
  DevAtlas.Infrastructure/  SQLite, filesystem discovery, and Git integration
  DevAtlas.App/             Avalonia desktop application and composition root
tests/
  DevAtlas.Tests/            Unit and integration-oriented tests
docs/
  ROADMAP.md                 Product milestones and engineering direction
.github/
  workflows/ci.yml           Restore, build, and test workflow
  dependabot.yml             Dependency update configuration
```

`.workspace/` is reserved for tracked collaboration instructions and logs. Generated agent scratch files under that directory are ignored by Git.

## Architecture

`DevAtlas.Domain` contains persistence-independent models. `DevAtlas.Application` defines boundaries and coordinates scans, bounded Git inspection, identity preservation, and reconciliation. `DevAtlas.Infrastructure` implements SQLite, filesystem discovery, and safe Git process inspection. `DevAtlas.App` owns Avalonia presentation and dependency composition. OS-specific services can be added behind the application abstractions as the product grows.

## Security and privacy

Treat repositories as untrusted content. DevAtlas does not execute detected package scripts, Makefiles, shell scripts, or Docker commands automatically. Git inspection uses a sanitized configuration and environment, and discovery does not follow filesystem reparse points such as junctions or symbolic links.

The application scans only workspace roots explicitly selected by the user. It stores local project metadata, activity, and settings on the device and does not provide telemetry or upload repository contents. See [`SECURITY.md`](SECURITY.md) and [`SECURITY-REVIEW.md`](SECURITY-REVIEW.md) for requirements, findings, and release gates.

## Build a portable release

With PowerShell 7, Python 3, and the .NET 10 SDK installed:

```powershell
./eng/Publish-Release.ps1
```

Use `-OutputDirectory artifacts/release-check-2` for another build. Existing artifact directories are never overwritten. Packaging generates a self-contained Windows x64 ZIP, SHA-256 checksums, a resolved dependency inventory, original package notices, and hashes for packaged files. Test/build checks should pass before publishing. See [release validation](docs/RELEASE-0.1.0.md).

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before making changes. Keep commits focused and use Conventional Commit subjects such as:

```text
feat(discovery): add stable repository identity
fix(build): restore release solution mappings
docs(readme): document local development commands
```

Changes that affect security, dependencies, licensing, process execution, or filesystem access require tests and documentation updates. Do not add AI/LLM services, telemetry, accounts, or mandatory network dependencies without an explicit product decision.

## License

DevAtlas is released under the MIT License. See [`LICENSE`](LICENSE) and [`LEGAL.md`](LEGAL.md) for third-party dependency and distribution obligations.
