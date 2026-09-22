# Changelog

All notable changes to DevAtlas will be documented here.

## Unreleased

### Added

- Initial .NET 10 solution architecture.
- Avalonia desktop shell.
- SQLite persistence for workspace roots and projects.
- Asynchronous local project discovery.
- Basic technology detection.
- Local Git metadata inspection.
- Dashboard search and workspace rescanning.
- Product roadmap, security policy, legal notes, and contribution guidance.

### Known limitations

- The current Git inspection path still needs isolation from repository-controlled Git helpers.
- Discovery still needs explicit reparse-point containment protection.
- The .NET SDK must be installed before restore, build, and test validation can run.
