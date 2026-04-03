# 01 - PR Authoring Guide (Cross-Project)

> Scope: these rules apply to all projects in this repo.

---

## Goal

PR description must be readable, reviewable, and deploy-safe:
- no literal `\n` in text
- no broken escaped paths like `\tests/...` or `\railway ...`
- clear validation commands
- mergeability/conflict status checked before merge

---

## Required PR Description Structure

Use this exact section order:

1. `## Problem`
2. `## What changed`
3. `## Why this fixes it`
4. `## Local validation`

Each command in `Local validation` must be in backticks.

---

## Authoring Rules

1. Create/edit PR body from a Markdown file, not from inline escaped string.
2. On Windows PowerShell, prefer `gh pr create --body-file` and `gh pr edit --body-file`.
3. If `gh pr edit` fails because of token scopes, use `gh api ... --raw-field body=\"...\"`.
4. Before merge, verify PR body does not contain literal `\\n`.
5. Before adding more commits to an existing PR, verify the PR state with `gh pr view <number> --json state`.
6. Never continue work on a PR whose state is not `OPEN`. If the previous `dev` -> `master` PR is `MERGED` or `CLOSED`, create a new PR for the next batch of commits.
7. If `master` moved since the last PR, sync `dev` with `origin/master` before creating the new PR.
8. After every push intended for review, run `gh pr list --state open --base master --head dev --json number,state,url`. If the result is empty, create a new PR immediately.
9. Never say "PR updated" unless an explicit post-push check confirms that the target PR is still `OPEN`.
10. In this repo shell environment (`PowerShell`), do not use `&&` in terminal commands. Use separate commands, or a PowerShell-safe sequence with `;` and `$LASTEXITCODE` checks.
11. For `git` operations that write to the index (`add`, `commit`, `merge`, `rebase`), do not run parallel git commands in the same repo. Finish one write operation before starting the next to avoid `.git/index.lock` races.
12. For `dev` -> `master` PRs, merge using **Create a merge commit** only. Do not use **Squash and merge** or **Rebase and merge** for the long-lived `dev` branch.
13. After every merge from `dev` into `master`, immediately sync `dev` with `origin/master` before starting the next task or opening the next PR.

---

## Pre-Merge Checks

Run:

```bash
gh pr view <number> --json body,mergeable,mergeStateStatus
gh pr checks <number>
gh pr list --state open --base master --head dev --json number,state,url
```

Expected:
- `mergeable = MERGEABLE`
- no merge conflicts
- required checks green (or explicitly approved exception)
- PR body is formatted as Markdown (real line breaks, no escaped junk)
- PR state is `OPEN` before editing/updating that PR
- an `OPEN` `dev` -> `master` PR exists after the last push; otherwise create a new one
- the planned merge strategy is **Create a merge commit**

---

## Safe Template

```markdown
## Problem
<what is broken and impact>

## What changed
- <change 1>
- <change 2>

## Why this fixes it
<short technical reason>

## Local validation
- `<command 1>`
- `<command 2>`
```

---

## Notes

- One PR = one logical change.
- Do not include unrelated refactors in deploy-fix PRs.

---

## Merge Runbook

Use this exact sequence for the long-lived `dev` branch:

1. Make changes in `dev`
2. Push `dev`
3. Open or update the `dev` -> `master` PR
4. Wait for checks to pass and resolve conflicts if needed
5. Merge with **Create a merge commit**
6. Pull `origin/master` back into `dev`
7. Push `dev`
8. Confirm there is no stale open PR before starting the next batch

Why this is mandatory:
- `Squash and merge` creates a new commit in `master` that does not exist in `dev`
- that makes `master` look "ahead" even in solo development
- the next PR then accumulates avoidable conflicts that we created ourselves
