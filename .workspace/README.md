# Collaboration workspace

This directory contains the small amount of collaboration state that should be shared with the repository. `README.md` describes the working agreement, and `agent-log.md` records concise, dated handoffs. Generated scratch files, temporary reports, and runtime agent artifacts remain ignored by the repository rules.

## Working agreement

Human maintainers own product decisions, release approval, and the final interpretation of security, legal, and licensing risk. Coding agents may inspect the repository, implement a scoped change, run validation, and prepare a commit or pull request. An agent must state assumptions and blockers rather than silently changing scope.

Before starting work, inspect the current branch, status, recent commits, open pull requests, and the relevant project documentation. Check the shared log for active work and known blockers. Avoid editing files that belong to another active change, and do not modify an open pull request unless its owner has asked for that work.

Keep changes focused and use the repository's existing patterns. Use Conventional Commits with a short imperative subject. Include the required Copilot co-author trailer on commits made by an agent:

```text
Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>
```

Do not rewrite, squash, or amend another contributor's commits. Before committing, inspect the diff and confirm that generated files, credentials, and unrelated changes are absent. Pull requests should state the behavior changed, validation performed, security or legal implications, and known limitations.

## Status and shared log

Use Git to inspect status and history:

```powershell
git status --short --branch
git log -5 --oneline --decorate
git diff --check
```

After meaningful work, append a short entry to `agent-log.md`. Include the date, the change or decision, the relevant branch or pull request, validation commands and their results, and any blocker or follow-up owner. Do not copy secrets, full environment values, or unnecessary repository contents into the log. Keep entries factual and append-only unless correcting a clear factual error.

## Dependencies and design boundaries

DevAtlas is offline-first. Do not add AI or LLM dependencies, telemetry, account requirements, network services, or automatic command execution without an explicit product decision documented by a maintainer. Dependency changes must explain the reason, review transitive effects, preserve the supported SDK policy, and include the strongest available restore, build, test, and vulnerability checks.

## Security, legal, and licensing review

Treat repository files, manifests, Git configuration, command definitions, and Docker files as untrusted input. Follow `SECURITY.md` for path containment, process isolation, secret handling, and vulnerability reporting. Security concerns must be reproduced and reviewed privately; do not publish exploit details or credentials in commits, logs, issues, or pull requests.

Follow `LEGAL.md` for dependency inventories, license notices, redistribution terms, privacy statements, and release review. Agents may identify and document risks, but maintainers must make legal, compliance, licensing, and release decisions. A passing build is not a security or legal certification.

## Validation record

Record the exact commands that were run and whether each passed, failed, or was blocked. Prefer the repository's documented checks:

```powershell
dotnet restore
dotnet build DevAtlas.sln
dotnet test DevAtlas.sln
git diff --check
```

If a command cannot run because a required SDK, tool, package, or service is unavailable, record the concrete blocker and do not report the check as passed. Keep validation limitations visible in the pull request and shared log.
