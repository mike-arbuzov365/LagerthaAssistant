# LagerthaAssistant.Web

Telegram Mini App frontend for LagerthaAssistant.

## Scripts

- `npm run dev` — local Vite dev server.
- `npm run build` — production build to `../LagerthaAssistant.Api/wwwroot/miniapp`.
- `npm run test:run` — run unit tests once.

## Current scope (M1)

- Mini App shell.
- Telegram/browser host adapter.
- Bootstrap loading from `/api/session/bootstrap`.
- Init-data verify call to `/api/miniapp/auth/verify`.
- Base navigation: `Dashboard`, `Settings`.
