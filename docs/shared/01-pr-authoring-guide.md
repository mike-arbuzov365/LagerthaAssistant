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
