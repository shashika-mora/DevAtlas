# Security policy

## Scope

DevAtlas is a local desktop application that reads directories explicitly selected by the user and stores its own metadata locally. Projects, manifests, Git repositories, command definitions, and Docker files must be treated as untrusted input.

## Current review status

A focused review of the current discovery and Git implementation identified two issues that must be addressed before a security-sensitive release:

| Severity | Area | Status |
|---|---|---|
| High | Git inspection may honor repository-controlled Git helpers such as `core.fsmonitor` | Open |
| Medium | Discovery may follow directory links/reparse points outside the selected workspace root | Open |

No hardcoded credentials, repository-content uploads, or obvious network exfiltration were found in the reviewed code. This is not a security certification.

## Security requirements

- Scan only roots explicitly selected by the user.
- Treat symlinks and Windows reparse points as boundaries unless containment is verified.
- Do not execute scripts merely because a manifest, Makefile, Gradle file, or repository contains them.
- Use structured process arguments and a sanitized environment.
- Do not allow Git inspection of untrusted repositories to invoke repository-configured helpers.
- Require confirmation before destructive Git, process, Docker, or filesystem operations.
- Never log secrets, full environment variables, credentials, or unnecessary repository contents.
- Keep all core functionality working without network access.
- Store local data with the least privilege available to the platform.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Report the affected component, reproduction steps, impact, and a proposed mitigation privately to the repository maintainers. Do not include credentials or private repository contents in a report.

Reports should be acknowledged, reproduced, fixed, and disclosed responsibly. Security fixes should include a regression test where practical.

## Release gate

Before a production release, the open findings above must be closed or explicitly accepted by a maintainer with a documented mitigation. Dependency review, secret scanning, static analysis, and tests for path containment and process isolation are required release checks.
