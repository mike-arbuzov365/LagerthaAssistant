# BaguetteDesign - One-shot Command Center

> Single entry point for launch-day preparation.
> Hard gate: explicit owner approval is mandatory before any Figma MCP connection.

---

## 1. What Is Ready Offline

Prepared sources:

1. `docs/08-design-system.md`
2. `docs/11-figma-one-shot-runbook.md`
3. `docs/12-wave1-screen-blueprint.md`
4. `docs/14-ux-copy-wave1-ua.md`
5. `docs/15-component-specs-wave1.md`
6. `docs/16-interaction-matrix-wave1.md`
7. `docs/17-batch-execution-script.md`
8. `docs/figma-queue.md`

---

## 2. Quick Start (Offline Validation)

1. Run automated preflight:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\figma-one-shot-preflight.ps1
```

2. Fill dry-run report:

- `docs/18-one-shot-dry-run-report.md`

3. Confirm owner-input blockers:

- Target Figma file URL/file key
- Explicit approval phrase in exact template format

---

## 3. Exact Approval Template

`АПРУВ MCP FIGMA: дозволяю одне підключення для батч-застосування QUEUE-001...QUEUE-017 у файлі [file-key/url].`

Without this exact format, launch is blocked.

---

## 4. Launch-Day Execution Order

1. Re-run preflight script.
2. Verify all launch conditions from `docs/13-one-shot-preflight-checklist.md`.
3. Execute one-shot using `docs/17-batch-execution-script.md`.
4. Update post-run artifacts in `docs/figma-queue.md`.

---

## 5. Stop Conditions

Stop immediately and return to offline mode if:

1. No edit access to target file.
2. Permission/rate-limit blocks execution.
3. Critical mismatch between queue scope and source-of-truth docs.
4. Any request appears outside `QUEUE-001...QUEUE-017` during the same run.

