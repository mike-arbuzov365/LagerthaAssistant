# План роботи: LagerthaAssistant — сесія Claude Code

> Дата: 2026-04-05
> Репо: `BotPlatform.sln` → фокус виключно на `LagerthaAssistant.*` і `SharedBotKernel`
> Мова: C# / .NET 10 | React + TypeScript (Vite)
> Тести зараз: 951 зелені (3 вимагають Docker)

---

## Правила сесії (нагадування перед стартом)

- Всі зміни — тільки в гілці `dev`
- PowerShell: **не** чейнити команди через `&&`, тільки окремі команди
- Після кожного блоку змін: `dotnet build BotPlatform.sln` → `dotnet test BotPlatform.sln -v minimal`
- Один PR = одна логічна одиниця. PR `dev → master`, merge commit only
- Тести: фейки hand-written (inner classes), без Moq

---

## КРОК 1 — Читання та синхронізація документації

**Мета:** Claude Code повинен мати актуальний контекст перед будь-якими змінами в коді.

### 1.1 Прочитати обов'язково (по порядку)

```
docs/05-development-workflow.md   ← правила git, PR, commit style
docs/06-testing.md                ← стан тестів, pyramid, fake pattern
docs/lagertha/01-backlog.md       ← активний backlog: Mini App performance, theme presets
docs/07-deploy.md                 ← поточний стан деплою, Railway, Dockerfiles
README.md                         ← схема БД, конфігурація, API endpoints
```

### 1.2 Прочитати для контексту архітектури

```
docs/03-architecture.md           ← monorepo структура, C4 діаграми
docs/shared/                      ← якщо є файли в цій папці
docs/platform/adr/                ← ADR рішення (якщо є)
```

### 1.3 Перевірити поточний стан

```powershell
# Стан гілок
git status
git log --oneline -10

# Перевірка що все білдиться
dotnet build BotPlatform.sln

# Тести (без Docker — тільки unit)
dotnet test tests/LagerthaAssistant.Application.Tests -v minimal
dotnet test tests/LagerthaAssistant.Domain.Tests -v minimal
dotnet test tests/SharedBotKernel.Tests -v minimal
```

**Очікуваний результат:** 520 тестів зелені (491 Application + 5 Domain + 24 SharedBotKernel).
Якщо є регресії — зупинитись і зафіксувати у звіті до переходу далі.

---

## КРОК 2 — Код-рев'ю LagerthaAssistant (.NET бекенд)

**Мета:** Знайти реальні проблеми, слабкі місця, прогалини в тестах.
Отримати: **звіт у форматі markdown** + **пріоритизований план виправлень**.

> ⚠️ Окремого .NET скіла в системі немає. Claude Code використовує вбудовані знання
> .NET 10 / Clean Architecture / EF Core / ASP.NET Core.
> Якщо потрібен скіл — створити через `skill-creator` перед стартом.

### 2.1 Шари для перевірки (по порядку)

#### Domain Layer
```
src/LagerthaAssistant.Domain/
```
Перевірити:
- Чи всі entities мають правильну інкапсуляцію (private setters де потрібно)
- Value Objects: чи є де вони потрібні але відсутні
- Domain events: чи використовуються, чи потрібні
- Порушення DDD: business logic що не належить до domain

#### Application Layer
```
src/LagerthaAssistant.Application/Services/Agents/
src/LagerthaAssistant.Application/Services/Vocabulary/
src/LagerthaAssistant.Application/Services/Food/
src/LagerthaAssistant.Application/Services/Memory/
src/LagerthaAssistant.Application/Interfaces/
```
Перевірити:
- `ConversationOrchestrator.cs` — складність, single responsibility
- `ConversationIntentRouter.cs` — routing logic, edge cases
- `ConversationAgentBoundaryPolicy.cs` — boundary checks повні?
- `AssistantSessionService.cs` — async correctness, ConfigureAwait не потрібен в app code
- `VocabularyConversationAgent.cs` — покриття тестами, edge cases
- `FoodTrackingConversationAgent.cs` — аналогічно
- Interfaces: чи немає Interface Segregation порушень

#### Infrastructure Layer
```
src/LagerthaAssistant.Infrastructure/
```
Перевірити:
- EF Core конфігурації: правильні індекси, nullable correctness
- Repository implementations: N+1 проблеми, зайві `.ToList()`
- Notion HTTP client: retry logic, timeout handling
- Graph HTTP client: token refresh flow, error handling
- Background workers: exception handling, graceful shutdown, IServiceScopeFactory usage

#### API Layer
```
src/LagerthaAssistant.Api/
```
Перевірити:
- Controller endpoints: валідація вхідних даних
- Telegram webhook: deduplication coverage, error handling
- `Program.cs` / DI: правильний service lifetime (Singleton/Scoped/Transient)
- Health check: що перевіряє, чи достатньо

### 2.2 Перевірка тестового покриття

```
tests/LagerthaAssistant.Application.Tests/    ← 491 тест — переглянути які агенти покриті
tests/LagerthaAssistant.Domain.Tests/         ← 5 тестів — що не покрито в domain?
tests/LagerthaAssistant.IntegrationTests/     ← 330 тестів — всі міграції перевірені?
```

Завдання:
- Знайти handlers/services з 0 або 1 тестом
- Знайти складну бізнес-логіку без тестів
- Перевірити чи нові фічі (локаль, Mini App settings commit) покриті тестами

### 2.3 Специфічні ризики для перевірки

```csharp
// 1. locale_selected_manually flag — чи завжди правильно виставляється?
// Файл: десь в UserLocaleStateService.cs або VocabularyConversationAgent.cs

// 2. Sync-over-async — патерн .Result або .GetAwaiter().GetResult()
// Шукати: grep -r "\.Result\b" src/LagerthaAssistant.*

// 3. Null safety — особливо в Telegram update parsing
// Перевірити: message?.From?.LanguageCode handling

// 4. HTML parse mode — чи всі відповіді через SendTextAsync передають ParseMode.Html?

// 5. Russian locale — чи є де-небудь "ru" в локалях після рефакторингу?
// Шукати: grep -r '"ru"' src/LagerthaAssistant.*

// 6. ConversationIntentMetrics — чи записуються для всіх агентів?
```

### 2.4 Формат звіту (Claude Code має згенерувати)

Claude Code зберігає звіт у файл:
```
docs/lagertha/code-review-YYYY-MM-DD.md
```

Структура звіту:
```markdown
# Код-рев'ю LagerthaAssistant — YYYY-MM-DD

## Критичні проблеми (блокують PR)
- [ ] ...

## Важливі проблеми (виправити в цій сесії)
- [ ] ...

## Прогалини в тестах
- [ ] Service/Handler — що не покрито
- [ ] ...

## Технічний борг (backlog)
- [ ] ...

## Що в хорошому стані (не чіпати)
- ...
```

**Очікуваний результат:** Ти переглядаєш звіт, апрувуєш план → даєш команду "починай виправляти".

---

## КРОК 3 — Аналіз Telegram Mini App (React/TypeScript фронтенд)

**Мета:** Зрозуміти поточний стан, знайти UX проблеми, втрачену функціональність,
запропонувати покращення. Отримати: **звіт + пропозиції змін**.

> 📋 Перед початком: Claude Code читає скіл `/mnt/skills/public/frontend-design/SKILL.md`

### 3.1 Структура Mini App для аналізу

```
src/LagerthaAssistant.Web/
├── src/
│   ├── pages/
│   │   ├── SettingsPage.tsx              ← головний екран (690+ рядків)
│   │   ├── settings-page-presenter.ts   ← presenter logic, choice builders
│   │   ├── settings-page-utils.ts       ← Telegram bridge utils
│   │   └── settings-page-presenter.test.ts
│   ├── settings/
│   │   └── settings-schema.ts           ← module system: core | lagertha
│   ├── api/
│   │   └── client.ts                    ← API calls
│   ├── state/
│   │   └── appStore.ts                  ← Zustand store
│   ├── lib/
│   │   ├── locale.ts
│   │   ├── theme.ts
│   │   └── miniAppDiagnostics.ts
│   └── app/                             ← routing, app shell
├── package.json
└── vite.config.ts
```

### 3.2 Функціональний аналіз (feature parity check)

**Стара версія** (налаштування через Telegram кнопки, без web-екрану) мала:
- Вибір мови (uk / en)
- Вибір режиму збереження (ask / auto / off)
- Вибір режиму сховища (local / graph)
- OneDrive login/logout
- Статус Notion інтеграції

**Поточний Mini App** має:
- ✅ Мова інтерфейсу (uk / en) з прапорами
- ✅ Тема (system / light / dark)
- ✅ Режим збереження (ask / auto / off)
- ✅ Режим сховища (local / graph) — може бути locked policy
- ✅ AI Provider вибір (OpenAI / Claude)
- ✅ AI Model вибір (динамічний список)
- ✅ API Key управління (введення + remove stored key)
- ✅ OneDrive: старт/завершення логіну, logout, sync now, rebuild index, clear cache
- ✅ Notion: статус vocabulary + food потоків
- ✅ Online/Offline індикатор
- ✅ Unsaved changes detection + Telegram closing confirmation
- ✅ Локалізація uk/en

**Завдання Claude Code:**
- Прочитати `src/LagerthaAssistant.Web/src/` повністю
- Порівняти з описом старих налаштувань з `README.md` (секція Telegram settings)
- Зафіксувати: чи є втрачена функціональність?

### 3.3 UX/дизайн аналіз

Перевірити:
```
1. Навігація
   - Чи є спосіб повернутись на головне меню без збереження?
   - Чи зрозуміло де кнопка "Зберегти"? (savebar знизу)
   - Чи відчувається scroll-flow на мобільному Telegram?

2. Стан завантаження
   - Що показується якщо bootstrap не прийшов?
   - Чи є skeleton loaders або тільки текст-плейсхолдери?
   - Що відбувається при offline?

3. Error handling UX
   - status-banner: чи помітний? чи зникає автоматично?
   - Retry button при помилці завантаження — чи є?

4. Форма API Key
   - password field — чи є показати/приховати кнопка?
   - Checkbox "видалити збережений ключ" — чи інтуїтивно?

5. Integration cards
   - OneDrive: service actions ховаються в <details> — чи знаходимі?
   - Notion: тільки read-only — це задокументовано у UI?

6. Теми
   - Backlog: theme presets (System / Dark Modern / Solarized Dark / Light Modern / Solarized Light)
   - Зараз тільки 3 варіанти — це достатньо для V1?

7. Адаптивність
   - ChoiceGrid в compact mode — чи на маленьких екранах норм?
   - ChoiceGrid в stack mode — чи не ріжеться контент?
```

### 3.4 Технічний аналіз фронтенду

Перевірити:
```typescript
// 1. SettingsPage.tsx — 690+ рядків в одному файлі
//    Чи потрібно розбити на підкомпоненти?
//    Кандидати: <GeneralSection />, <AiSection />, <IntegrationsSection />

// 2. settings-schema.ts — module system (core | lagertha)
//    Чи використовується ця схема у SettingsPage.tsx?
//    Якщо ні — dead code?

// 3. copyByLocale в SettingsPage.tsx — величезний об'єкт в компоненті
//    Перемістити в presenter або окремий i18n файл?

// 4. useEffect chains — чи немає infinite loop ризиків?
//    Зокрема: bootstrap → locale sync → provider refresh → model refresh

// 5. Telegram bridge polling (tick з setTimeout)
//    Чи є memory leak якщо компонент unmount під час polling?

// 6. providerRequestVersion.current — race condition guard
//    Чи коректно обробляється якщо кілька requests in-flight?

// 7. Тести: settings-page-presenter.test.ts
//    Що покрито? Що не покрито?
//    settings-schema.test.ts — що тестується?
```

### 3.5 Пропозиції покращень (Claude Code генерує)

Формат: конкретні пропозиції з пріоритетами:

```
P0 — критично (UX broken або feature missing)
P1 — важливо (суттєво покращить досвід)
P2 — backlog (непогано мати, але не блокує)
```

### 3.6 Формат звіту (Claude Code зберігає у файл)

```
docs/lagertha/miniapp-review-YYYY-MM-DD.md
```

Структура:
```markdown
# Mini App Review — YYYY-MM-DD

## Feature Parity з старою версією
- ✅ / ❌ кожна функція

## UX проблеми
| Пріоритет | Опис | Де в коді |
|-----------|------|-----------|

## Технічний борг
| Пріоритет | Опис | Де в коді |
|-----------|------|-----------|

## Пропозиції покращень дизайну та навігації
(з посиланнями на backlog з docs/lagertha/01-backlog.md)

## Що добре зроблено (не чіпати)
```

**Очікуваний результат:** Ти переглядаєш звіт, вибираєш що виправляти → окрема сесія реалізації.

---

## Порядок виконання завтра

```
[Старт сесії]
     ↓
КРОК 1: Читання документації (15 хв)
  → git status, dotnet build, unit tests
     ↓
КРОК 2: .NET код-рев'ю (60–90 хв)
  → Читання Layer by layer
  → Генерація code-review-*.md
  → Очікування апруву від тебе
     ↓
[Твій апрув звіту]
     ↓
КРОК 3: Mini App аналіз (45–60 хв)
  → Читання frontend-design SKILL.md
  → Читання src/LagerthaAssistant.Web/src/
  → Генерація miniapp-review-*.md
  → Очікування апруву від тебе
     ↓
[Твій апрув звіту]
     ↓
[Окремі сесії виправлень — за окремими планами]
```

---

## Стартова команда для Claude Code

Скопіюй і вставте в Claude Code на початку сесії:

```
Ми починаємо сесію роботи з LagerthaAssistant.
Репо: BotPlatform.sln (монорепо). Фокус тільки на LagerthaAssistant.* і SharedBotKernel.
Baguette не чіпаємо.

Завдання сьогодні — три кроки за планом docs/lagertha/session-plan-2026-04-05.md

Починай з КРОКУ 1: прочитай документацію в тому порядку що вказаний у плані,
потім перевір git status і запусти unit тести.
Зупинись після КРОКУ 1 і дочекайся підтвердження перед тим як переходити до КРОКУ 2.
```

---

## Довідка: ключові файли

| Що шукати | Шлях |
|-----------|------|
| Orchestrator | `src/LagerthaAssistant.Application/Services/Agents/ConversationOrchestrator.cs` |
| Intent Router | `src/LagerthaAssistant.Application/Services/Agents/ConversationIntentRouter.cs` |
| Locale state | `src/LagerthaAssistant.Application/Services/UserLocaleStateService.cs` |
| Theme state | `src/LagerthaAssistant.Application/Services/UserThemeStateService.cs` |
| Navigation | `src/LagerthaAssistant.Application/Services/NavigationStateService.cs` |
| Bootstrap | `src/LagerthaAssistant.Application/Services/Agents/ConversationBootstrapService.cs` |
| Mini App entry | `src/LagerthaAssistant.Web/src/pages/SettingsPage.tsx` |
| Mini App store | `src/LagerthaAssistant.Web/src/state/appStore.ts` |
| Mini App API | `src/LagerthaAssistant.Web/src/api/client.ts` |
| Mini App bridge | `src/LagerthaAssistant.Web/src/pages/settings-page-utils.ts` |
| Lagertha backlog | `docs/lagertha/01-backlog.md` |
| Testing guide | `docs/06-testing.md` |
| Workflow | `docs/05-development-workflow.md` |
