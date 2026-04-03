# 05 — Development Workflow

> Solo developer + AI pair programming. Small steps, constant verification.

---

## Git Workflow

| Branch | Purpose |
|---|---|
| `master` | Always deployable — production state |
| `dev` | Main working branch — all changes go here |

**Rules:**
- Never push directly to `master`
- All work happens on `dev`
- Create PRs from `dev` → `master`
- One PR = one logical milestone (not one commit)
- No new branches without explicit instruction
- Before updating a PR, check that its state is still `OPEN`
- If the previous `dev` -> `master` PR is `MERGED` or `CLOSED`, open a new PR for later commits
- Sync `dev` with `origin/master` before opening the next PR if `master` moved
- After each review-intended push, verify that an `OPEN` `dev` -> `master` PR still exists; if not, create a new PR immediately
- Never report "PR updated" until that post-push check confirms the PR is still `OPEN`
- Merge `dev` -> `master` PRs with **Create a merge commit** only
- Do not use **Squash and merge** or **Rebase and merge** for the long-lived `dev` branch
- Immediately after every merge to `master`, sync `dev` with `origin/master` before starting the next task
- PR authoring/formatting rules: `docs/shared/01-pr-authoring-guide.md`
- In the current shell environment (`PowerShell`), do not chain commands with `&&`; use separate commands or PowerShell-safe sequencing with `$LASTEXITCODE`
- Do not run parallel `git` write operations in the same repo (`add`, `commit`, `merge`, `rebase`), because they can collide on `.git/index.lock`

**Commit convention:** `feat` / `fix` / `refactor` / `test` / `docs` / `chore`

```bash
# Typical session
git add src/BaguetteDesign.Application/Services/QuestionHandler.cs
git commit -m "feat: QuestionHandler with conversation history (M1 #013)"
git push origin dev
# → open PR dev → master
```

### Merge Runbook

Use this exact sequence:

```bash
# 1. Work only in dev
git push origin dev

# 2. Open/update PR dev -> master

# 3. In GitHub, merge with:
#    Create a merge commit

# 4. Sync dev back with master immediately after merge
git fetch origin
git checkout dev
git merge origin/master
git push origin dev
```

Why:
- `Squash and merge` creates a new commit in `master` that `dev` does not contain
- that makes `master` appear "ahead" even in solo development
- the next PR then hits unnecessary conflicts caused by the merge strategy itself

---

## AI-Assisted Development (Claude Code)

This project is built with Claude Code as the primary pair programming tool.

**How to give Claude context:**
- Reference the issue number: "Implement M1 #014 BriefFlowService"
- Point to existing analogues: "Follow the same pattern as QuestionHandler"
- State what NOT to do: "Don't add error handling for impossible cases"

**Rules we follow:**
- Always read generated code before committing
- One issue at a time — not "build the whole brief flow", but "implement step 1"
- After each block of code — build and test immediately
- If Claude is going in circles — restart with a clear context dump

**Code review prompt (use before merge):**
```
Review this PR.
Check:
1. Clean Architecture — no domain logic in Api layer
2. Null safety — any possible NullReferenceException?
3. Async correctness — no sync-over-async, ConfigureAwait not needed in app code
4. Test coverage — is the new business logic unit-tested?
5. Anything obviously wrong or overcomplicated?
```

---

## Project Structure

```
BotPlatform.sln
├── src/
│   ├── SharedBotKernel/          # Shared: entities, AI clients, Telegram sender
│   ├── LagerthaAssistant.Domain/
│   ├── LagerthaAssistant.Application/
│   ├── LagerthaAssistant.Infrastructure/
│   ├── LagerthaAssistant.Api/    # Household bot (production on Railway)
│   ├── BaguetteDesign.Domain/
│   ├── BaguetteDesign.Application/
│   ├── BaguetteDesign.Infrastructure/
│   └── BaguetteDesign.Api/       # Designer bot (M1 in progress)
└── tests/
    ├── LagerthaAssistant.Domain.Tests/
    ├── LagerthaAssistant.Application.Tests/
    ├── LagerthaAssistant.IntegrationTests/
    └── BaguetteDesign.Tests/
```

**Layer rules:**
- `Domain` — no external dependencies, pure C# records and entities
- `Application` — interfaces only, no EF, no HttpClient
- `Infrastructure` — EF, HTTP clients, Notion/Google adapters
- `Api` — controllers, Program.cs, DI registration

---

## Definition of Done (per issue)

- [ ] Code written and reviewed (with Claude)
- [ ] Unit tests for business logic
- [ ] `dotnet build BotPlatform.sln` — green
- [ ] `dotnet test BotPlatform.sln` — green
- [ ] Tested manually in Telegram (screenshot or video)
- [ ] PR merged `dev` → `master`
- [ ] Docs updated (this file or relevant doc)

---

## Working with Two Bots

Changes to `SharedBotKernel` affect **both** bots — always run the full solution build and all tests, not just the bot you're working on.

```bash
# Always run on the full solution
dotnet build BotPlatform.sln
dotnet test BotPlatform.sln
```
