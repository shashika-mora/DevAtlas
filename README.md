# DevAtlas

Offline-first developer workspace manager for discovering local projects, tracking Git state, managing runtimes and ports, running workflows, and resuming development from one desktop app.

## Current vertical slice

The repository contains a .NET 10 solution split into domain, application, infrastructure, desktop UI, and test projects. The initial slice provides:

- Avalonia desktop shell with offline-first navigation and dashboard
- SQLite persistence for workspace roots and discovered projects
- asynchronous, cancellable filesystem discovery with common directory exclusions
- project technology detection for Node.js, Java, Rust, Python, Go, PHP, and Docker signals
- local Git branch, status, changed-file, and recent-commit inspection
- project search, modified-file indicators, and workspace rescanning

The app stores its database under the platform local application-data directory in `DevAtlas/devatlas.db`. It does not upload repository contents or execute detected commands automatically.

## Build and test

```powershell
dotnet restore
dotnet build DevAtlas.sln
dotnet test DevAtlas.sln
```

The desktop entry point is `src/DevAtlas.App`.

## Architecture

`DevAtlas.Domain` contains persistence-independent models. `DevAtlas.Application` defines use-case boundaries. `DevAtlas.Infrastructure` implements SQLite, filesystem discovery, and safe Git process inspection. `DevAtlas.App` owns Avalonia presentation and dependency composition. OS-specific services can be added behind the application abstractions as the product grows.
