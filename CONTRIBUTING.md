# Contributing to DevAtlas

## Before making a change

Read the product direction in [`docs/ROADMAP.md`](docs/ROADMAP.md) and the security requirements in [`SECURITY.md`](SECURITY.md). Keep changes focused and prefer a complete vertical slice over speculative abstractions.

## Development expectations

- Keep the solution buildable.
- Add or update tests for behavior changes.
- Preserve offline-first behavior.
- Do not execute repository-provided commands automatically.
- Do not add network services, telemetry, accounts, or AI dependencies without an explicit product decision.
- Keep operating-system-specific code behind application abstractions.
- Update documentation when behavior, security, or licensing changes.

## Commit messages

Use Conventional Commits with a short imperative subject:

```text
feat(discovery): add stable repository identity
fix(security): prevent scans from leaving workspace roots
test(git): cover detached-head status parsing
docs(roadmap): define the v0.2 milestone
build(ci): add restore, build, and test workflow
```

Keep the subject under 72 characters where possible. Explain the reason and user-visible impact in the body when the change is not self-evident. Do not mix unrelated refactors into a feature or fix.

## Pull requests

Describe the behavior changed, tests run, security implications, and any known limitations. Include screenshots for substantial UI changes. A pull request is not ready until CI passes and the relevant documentation is updated.
