# DevAtlas

Offline-first developer workspace manager for discovering local projects, tracking Git state, managing runtimes and ports, running workflows, and resuming development from one desktop app.

DevAtlas is designed to remain useful without an account, an API key, a mandatory cloud service, or generative AI. Repository contents and DevAtlas-owned metadata stay on the local machine. Network access is reserved for explicitly requested operations such as Git fetch, pull, push, clone, and application updates.

## Current vertical slice

The repository contains a .NET 10 solution split into domain, application, infrastructure, desktop UI, and test projects. The initial slice provides:

- Avalonia desktop shell with offline-first navigation and dashboard
- SQLite persistence for workspace roots and discovered projects
- asynchronous, cancellable filesystem discovery with common directory exclusions
- project technology detection for Node.js, Java, Rust, Python, Go, PHP, and Docker signals
- local Git branch, status, changed-file, and recent-commit inspection
- project search, modified-file indicators, and workspace rescanning

The app stores its database under the platform local application-data directory in `DevAtlas/devatlas.db`. It does not upload repository contents or execute detected commands automatically.

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

`DevAtlas.Domain` contains persistence-independent models. `DevAtlas.Application` defines use-case boundaries. `DevAtlas.Infrastructure` implements SQLite, filesystem discovery, and safe Git process inspection. `DevAtlas.App` owns Avalonia presentation and dependency composition. OS-specific services can be added behind the application abstractions as the product grows.

## Security and privacy

Treat repositories as untrusted content. DevAtlas does not execute detected package scripts, Makefiles, shell scripts, or Docker commands automatically. Git inspection uses a sanitized configuration and environment, and discovery does not follow filesystem reparse points such as junctions or symbolic links.

The application scans only workspace roots explicitly selected by the user. It stores local project metadata, activity, and settings on the device and does not provide telemetry or upload repository contents. See [`SECURITY.md`](SECURITY.md) and [`SECURITY-REVIEW.md`](SECURITY-REVIEW.md) for requirements, findings, and release gates.

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
