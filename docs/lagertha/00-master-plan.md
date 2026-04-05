# LagerthaAssistant — Master Plan сесії Claude Code
> Дата: 2026-04-05 | Модель: Claude Opus 4.6 (рекомендовано)
> Репо: `BotPlatform.sln` → фокус виключно на `LagerthaAssistant.*` і `SharedBotKernel`
> Baguette не чіпаємо.

---

## Правила сесії (читати перед стартом)

- Всі зміни — тільки в гілці `dev`
- PowerShell: **не** чейнити команди через `&&`, тільки окремі команди
- Після кожного блоку змін: `dotnet build BotPlatform.sln` → `dotnet test BotPlatform.sln -v minimal`
- Один PR = одна логічна одиниця. PR `dev → master`, merge commit only
- Тести: фейки hand-written (inner classes), без Moq
- Фронтенд: `pnpm --dir src/LagerthaAssistant.Web test:run` після змін у Web
- Локалізація: inline-словники `English` / `Ukrainian` у `src/LagerthaAssistant.Infrastructure/Services/LocalizationService.cs`

---

## ФАЗА 0 — Аналіз і поліпшення плану (Opus виконує автономно)

**Мета:** Перш ніж писати код, Opus 4.6 має перечитати всі задачі, знайти суперечності,
неточності і потенційні проблеми в плані — і покращити їх. Зміни вносяться прямо
в файли плану (цей файл + `01-tasks-F01-F07.md`), без окремого analysis-файлу.

### 0.1 Прочитати всі файли плану

```
docs/lagertha/00-master-plan.md          ← цей файл
docs/lagertha/01-tasks-F01-F07.md        ← всі задачі F01–F07 в одному файлі
```

### 0.2 Прочитати документацію проекту

```
docs/05-development-workflow.md
docs/06-testing.md
docs/lagertha/02-backlog.md
docs/03-architecture.md
README.md
```

### 0.3 Що перевірити в плані

- Чи всі файли/методи в задачах реально існують у репо (перевірити структуру)
- Чи немає суперечностей між задачами (наприклад F01 і F03 обидві чіпають Settings flow)
- Чи правильно оцінені пріоритети
- Чи не дублюється логіка між задачами
- Чи не пропущені залежності між задачами (порядок виконання)
- Чи acceptance criteria реалістичні

### 0.4 Що зробити після аналізу

Внести виправлення прямо у файли плану. Не створювати окремих analysis-файлів.
Надати стислий звіт користувачу.

Після аналізу — перейти до ФАЗИ 1 автоматично.

---

## ФАЗА 1 — Верифікація середовища (виконується автономно)

### 1.1 Перевірка стану репо

```powershell
git status
git log --oneline -5
```

### 1.2 Build

```powershell
dotnet build BotPlatform.sln
```

**Очікувано:** 0 errors. Якщо є — зупинитись, зафіксувати.

### 1.3 Юніт-тести (без Docker)

```powershell
dotnet test tests/LagerthaAssistant.Application.Tests -v minimal
dotnet test tests/LagerthaAssistant.Domain.Tests -v minimal
dotnet test tests/SharedBotKernel.Tests -v minimal
```

### 1.4 Фронтенд

```powershell
pnpm --dir src/LagerthaAssistant.Web test:run
```

**Очікувано:** всі тести зелені. Якщо є failures — зафіксувати.

Після ФАЗИ 1 → перейти до ФАЗИ 2 автоматично.

---

## ФАЗА 2 — Код-рев'ю (виконується автономно → 1 апрув)

Мета: зрозуміти поточний стан коду перед будь-якими змінами.
Результат: один зведений звіт. **Після нього — апрув перед переходом до ФАЗИ 3.**

### 2.1 Зведений код-рев'ю (.NET + Mini App)

**Шари .NET для перевірки:**
```
src/LagerthaAssistant.Domain/
src/LagerthaAssistant.Application/Services/Agents/
src/LagerthaAssistant.Application/Services/Vocabulary/
src/LagerthaAssistant.Application/Services/Food/
src/LagerthaAssistant.Infrastructure/
src/LagerthaAssistant.Api/Controllers/TelegramController.cs
src/LagerthaAssistant.Api/Services/TelegramNavigationPresenter.cs
```

**Специфічно перевірити:**
- `ConversationOrchestrator.cs` — single responsibility
- `TelegramController.cs` — розмір (6000+ рядків), чи є шанс розбити
- Async correctness: відсутність `.Result` / `.GetAwaiter().GetResult()`
- Null safety в Telegram update parsing
- Покриття тестами: Application.Tests / Domain.Tests

**Mini App файли для перевірки:**
```
src/LagerthaAssistant.Web/src/pages/SettingsPage.tsx
src/LagerthaAssistant.Web/src/pages/settings-page-utils.ts
src/LagerthaAssistant.Web/src/pages/settings-page-presenter.ts
src/LagerthaAssistant.Web/src/settings/settings-schema.ts
src/LagerthaAssistant.Web/src/state/appStore.ts
src/LagerthaAssistant.Web/src/api/client.ts
```

Зберегти зведений звіт:
```
docs/lagertha/code-review-YYYY-MM-DD.md
```

---

## CHECKPOINT 1 — Апрув після ФАЗИ 2

**Зупинитись. Надіслати звіт Майку.**

Майк читає `code-review-*.md`, підтверджує пріоритети і дає команду рухатись далі.

**Після апруву → ФАЗА 3.**

---

## ФАЗА 3 — Виправлення багів (виконується автономно)

Виконувати по порядку. Кожна задача = окремий commit + build + tests.

### Порядок виконання (з урахуванням залежностей)

| # | Задача | Файл задачі | Пріоритет | Залежність |
|---|--------|-------------|-----------|------------|
| 1 | Settings: пряме відкриття Mini App | F01 | BROKEN | — |
| 2 | Vocabulary URL: фільтрація дублікатів | F02 | WRONG | — |
| 3 | Settings: 3-action close dialog | F03 | UX | F01 |
| 4 | Navigation: input-only keyboard + rename | F04 | UX | — |
| 5 | Weekly menu: compact view + icons | F05 | UX Enhancement | — |

**Правила виконання кожної задачі:**

```
1. Прочитати задачу в 01-tasks-F01-F07.md
2. Знайти і прочитати релевантний код у репо
3. Реалізувати рішення
4. Написати/оновити тести (hand-written fakes, inner classes)
5. dotnet build BotPlatform.sln
6. dotnet test BotPlatform.sln -v minimal
7. Якщо є фронтенд-зміни: pnpm --dir src/LagerthaAssistant.Web test:run
8. git add + git commit (один commit на задачу)
9. Перейти до наступної задачі
```

**Commit message format:**
```
fix: settings button opens mini app directly without intermediate menu (F01)
fix: filter known words from vocabulary url import candidate list (F02)
feat: replace native closing confirmation with 3-action centered dialog (F03)
fix: show only back button in input-only navigation scenarios + rename to Back (F04)
feat: compact grouped weekly menu view with icons and categories (F05)
```

Після всіх задач ФАЗИ 3:
```powershell
dotnet test BotPlatform.sln -v minimal
```
```powershell
pnpm --dir src/LagerthaAssistant.Web test:run
```
```powershell
git push origin dev
```

Відкрити PR: `dev → master`.

---

## CHECKPOINT 2 — Апрув перед ФАЗОЮ 4

**Зупинитись. Повідомити Майка що PR з багфіксами готовий.**

Майк переглядає PR, апрувить або дає коментарі.
Після merge → ФАЗА 4.

---

## ФАЗА 4 — Нові фічі (окрема сесія)

Ці задачі великі. Виконувати в окремій сесії після merge ФАЗИ 3.

| # | Задача | Файл задачі | Складність | Залежність |
|---|--------|-------------|-----------|------------|
| 1 | Media intent selection (фото/файли) | F06 | Висока | F04 (navigation patterns) |
| 2 | Gemini AI provider | F07 | Висока | — |

Для кожної фічі: окремий PR після кожної.

---

## Довідка: ключові файли

| Що шукати | Шлях |
|-----------|------|
| Navigation presenter | `src/LagerthaAssistant.Api/Services/TelegramNavigationPresenter.cs` |
| Telegram controller | `src/LagerthaAssistant.Api/Controllers/TelegramController.cs` |
| Orchestrator | `src/LagerthaAssistant.Application/Services/Agents/ConversationOrchestrator.cs` |
| Navigation router | `src/LagerthaAssistant.Application/Navigation/NavigationRouter.cs` |
| Callback constants | `src/LagerthaAssistant.Application/Constants/CallbackDataConstants.cs` |
| Nav sections | `src/LagerthaAssistant.Application/Constants/NavigationSections.cs` |
| Vocabulary index | `src/LagerthaAssistant.Application/Services/Vocabulary/VocabularyIndexService.cs` |
| AI provider constants | `src/LagerthaAssistant.Application/Constants/AiProviderConstants.cs` |
| Localization service | `src/LagerthaAssistant.Infrastructure/Services/LocalizationService.cs` |
| Slash commands | `src/LagerthaAssistant.Application/Constants/ConversationSlashCommands.cs` |
| Pending state models | `src/LagerthaAssistant.Api/Models/PendingTelegramModels.cs` |
| Mini App Settings page | `src/LagerthaAssistant.Web/src/pages/SettingsPage.tsx` |
| Mini App presenter | `src/LagerthaAssistant.Web/src/pages/settings-page-presenter.ts` |
| Mini App bridge utils | `src/LagerthaAssistant.Web/src/pages/settings-page-utils.ts` |
| Mini App store | `src/LagerthaAssistant.Web/src/state/appStore.ts` |
| Backlog | `docs/lagertha/02-backlog.md` |
| Testing guide | `docs/06-testing.md` |
| Workflow guide | `docs/05-development-workflow.md` |
