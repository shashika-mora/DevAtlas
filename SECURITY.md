# Security policy

## Scope

DevAtlas is a local desktop application that reads directories explicitly selected by the user and stores its own metadata locally. Projects, manifests, Git repositories, command definitions, and Docker files must be treated as untrusted input.

## Current review status

The 0.1.0 review and regression evidence are recorded in `SECURITY-REVIEW.md` and `docs/RELEASE-0.1.0.md`. Git helper/environment isolation and directory-link boundaries have regression coverage. These controls do not provide an OS sandbox or a security certification.

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

Before a production release, dependency review, secret scanning, static analysis, and regression tests for path containment and process isolation are required release checks.
