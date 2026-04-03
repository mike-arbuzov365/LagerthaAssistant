# Lagertha Backlog

## UX / Mini App

### Theme Presets for Settings

Status: Backlog, not started.

Context:
- The current theme selector should remain functional and stable first.
- Theme preset exploration should be done separately from critical save/close/confirm fixes.

Planned presets:
- `System`
- `Dark Modern`
- `Solarized Dark`
- `Light Modern`
- `Solarized Light`

Implementation notes:
- Do not create a separate long-lived branch for this experiment.
- Before implementation, create a clear checkpoint commit in `dev`.
- Implement theme presets in a small isolated series of commits.
- If the result is not approved, revert only those theme commits with `git revert`.
- Do not mix this work with unrelated Mini App runtime or Telegram integration fixes.

UX notes:
- Reuse one visual language for theme cards and integration cards.
- Prefer real flag/icon assets and consistent icon style across mobile and desktop.
- Theme names should follow common product language; keep `System` as the default system-following option.

