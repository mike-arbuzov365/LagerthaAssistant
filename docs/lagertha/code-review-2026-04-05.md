# Code Review — 2026-04-05

## Environment Baseline

| Check | Result |
|-------|--------|
| Build | 0 errors, 0 warnings |
| Application.Tests | 512 passed |
| Domain.Tests | 5 passed |
| SharedBotKernel.Tests | 61 passed |
| Frontend tests | 49 tests passed (2 test files crash on TWA SDK import — pre-existing jsdom issue) |
| **Total** | **578 .NET + 49 frontend** |

---

## .NET Backend

### CRITICAL — TelegramController.cs (6,611 lines)

Single sealed class, not partial. Handles webhook routing, state management, keyboard building,
UI response formatting, user preferences — violates SRP.

**No unit tests exist for this controller.** This is the largest coverage gap.

**Recommendation:** Extract into focused services (routing, keyboard building, state management).
Not in scope for current sprint (F01–F07), but important for future.

### Good — ConversationOrchestrator.cs (203 lines)

Clean orchestrator/router. 6 dependencies (reasonable). Uses noop defaults instead of nulls.
Strategy pattern with `IConversationAgent`. No issues.

### Good — TelegramNavigationPresenter.cs (477 lines)

All keyboard builders follow consistent pattern (locale param, localized labels, helper methods).
`BuildSettingsLaunchKeyboard()` correctly prioritizes DirectUrl > WebAppUrl > Legacy.
**No tests** for this class — gap, but low risk since it's mostly declarative.

### Good — VocabularyIndexService.FindByInputsAsync()

Correct implementation. Proper null checks, normalization, deduplication, single batch DB call.
Well-tested in `VocabularyIndexServiceTests.cs`.

### Good — AiRuntimeSettingsService.cs (376 lines)

Secure key handling via `IAiSecretProtector`. Clear fallback chain (user key > env key > missing).
Provider normalization handles aliases correctly.

### WARNING — FoodTrackingConversationAgent.HandleWeeklyViewAsync

`CancellationToken` parameter declared but not passed to inner calls. Minor.

### Good — LocalizationService.cs (850 lines, 393+ keys)

Complete en/uk coverage. Safe fallback `[?:key]` for missing keys.
Inline dictionaries — adding keys is straightforward.

### Async Safety

No `.Result` or `.GetAwaiter().GetResult()` found anywhere. Null safety is consistent
(null-coalescing, guard clauses throughout).

### Test Coverage Summary (.NET)

| Area | Status |
|------|--------|
| ConversationOrchestrator | Tested |
| Navigation router | Tested |
| Vocabulary services | Well tested (11 test files) |
| Food services | Tested (3 test files) |
| Localization | Tested |
| AI settings | Tested |
| TelegramController | **NOT tested** |
| TelegramNavigationPresenter | **NOT tested** |

---

## Mini App Frontend

### WARNING — SettingsPage.tsx (1,686 lines)

20+ useState calls. Acts as god-component managing UI state, form drafts, API responses,
async tracking. Works correctly but is complex.

**No component-level tests.** Utilities are well-tested, but the component itself is not.

Good patterns:
- `useMemo` for `hasUnsavedChanges` (9 dependencies)
- Ref-based request versioning prevents race conditions
- Proper effect cleanup

### INFO — settings-page-utils.ts

Telegram SDK bridge properly wrapped with defensive patterns:
- `waitForTelegramMiniAppBridge()` with polling timeout
- `applyTelegramClosingConfirmation()` handles both API and WebView fallback
- `syncTelegramClosingConfirmation()` re-applies at 0ms and 120ms (fragile but functional)

### WARNING — settings-page-presenter.ts

`formatProviderLabel()` only handles `openai` and `claude`.
No Gemini support yet — will need update for F07.

### Good — appStore.ts (Zustand)

Clean single store with minimal action set. Proper partial updates.

### Good — client.ts

Consistent error handling (throws on non-200). All 30+ API functions follow same pattern.
Server-Timing header parsing for performance measurement.
Minor: error messages lack response body (status only).

### Frontend Test Coverage

9 test files, 49 tests, all passing:
- Locale normalization, settings changes detection, Telegram bridge handling,
  presenter functions, host detection, schema, store — all covered.
- **SettingsPage component — NOT tested.**

---

## Relevance to F01–F07

| Task | Review Finding |
|------|---------------|
| F01 | `BuildSettingsLaunchKeyboard()` reviewed, logic clear. Handler at line 511. |
| F02 | `FindByInputsAsync()` reviewed, correct. `TryHandleVocabularyImportFlowAsync` at line 1894. |
| F03 | Telegram closing confirmation utils reviewed. `syncTelegramClosingConfirmation` uses hardcoded delays. |
| F04 | `PendingChatActionKind` enum has 7 values. Navigation keyboard builders are consistent. |
| F05 | `HandleWeeklyViewAsync` reviewed. `MapToMeal()` maps minimal fields — needs Category/IconEmoji. |
| F06 | `DetectSourceTypeFromInbound()` at line 2223. Media routing exists but is scattered. |
| F07 | Duplicate `AiProviderConstants.cs` confirmed. `formatProviderLabel()` needs Gemini case. |

---

## Overall Assessment

**Backend: 7.5/10** — Sound architecture, good test coverage for business logic.
Main debt: monolithic TelegramController (6,611 lines, no tests).

**Frontend: 7/10** — Working well, good utility test coverage.
Main debt: god-component SettingsPage (1,686 lines, no component tests).

**No blockers for F01–F07 execution.**
