# F01 — Settings: пряме відкриття Mini App без проміжного меню

**Пріоритет:** 🔴 BROKEN
**Тип:** UX / Navigation
**Залежності:** немає

---

## Проблема

Натискання «⚙️ Налаштування» у reply-клавіатурі відкриває проміжне повідомлення
з трьома кнопками замість того, щоб одразу відкрити Mini App.

**State before:** /start → головне меню → натискаємо «Налаштування»
**Action:** тап кнопки
**Result:** бот надсилає вибір: «Відкрити налаштування / Відкрити стару inline-панель / Головне меню»
**Expected:** Mini App відкривається одразу

---

## Де в коді

**1. Keyboard будується тут:**
`src/LagerthaAssistant.Api/Services/TelegramNavigationPresenter.cs`
→ метод `BuildSettingsLaunchKeyboard()` — три кнопки

**2. Тут обробляється натискання кнопки Налаштування:**
`src/LagerthaAssistant.Api/Controllers/TelegramController.cs`
→ обробка `NavigationRouteKind.MainSettingsButton` (~рядок 511)
→ зараз викликає `BuildSettingsLaunchKeyboard()` якщо є Mini App URL

**3. Флаг для перевірки:**
`TelegramNavigationPresenter.CanLaunchSettingsMiniApp` — `true` якщо є URL

**4. Legacy callback:**
`src/LagerthaAssistant.Application/Constants/CallbackDataConstants.cs`
→ `Settings.Legacy = "settings:legacy"` — залишити, але прибрати з головного флоу

---

## Рішення

**Крок 1.** В обробнику `NavigationRouteKind.MainSettingsButton`:
- якщо `CanLaunchSettingsMiniApp == true` → надіслати повідомлення відразу
  з однією кнопкою Mini App, без проміжного вибору
- якщо `CanLaunchSettingsMiniApp == false` → fallback: стара inline-панель (без змін)

**Крок 2.** Стара inline-панель → перенести на `/legacy`:
- додати обробку slash-команди в `TelegramController.cs`
- зареєструвати в `ConversationSlashCommands.cs` (поточні команди: help, history, memory, prompt, sync, reset, index)

**Крок 3.** `BuildSettingsLaunchKeyboard()` — залишити як private/internal
для fallback сценарію або видалити якщо більше не потрібен.

---

## Acceptance Criteria

- [ ] «Налаштування» → Mini App відкривається одразу (якщо є URL)
- [ ] `/legacy` → стара inline-панель
- [ ] Якщо Mini App не налаштований → стара панель як fallback (без змін)
- [ ] Локалізація uk/en — додати ключі в `LocalizationService.cs` (inline-словники `English` / `Ukrainian`)
- [ ] Юніт-тест для fallback path
- [ ] Ручний тест у Telegram: один тап → Mini App

## Commit message
```
fix: settings button opens mini app directly without intermediate menu (F01)
```


---
---
# F02 — Vocabulary URL-імпорт: дублікати не фільтруються до показу списку

**Пріоритет:** 🟡 WRONG
**Тип:** Business Logic / Vocabulary
**Залежності:** немає

---

## Проблема

При імпорті слів за посиланням бот показує слова що **вже є в словнику**
у списку кандидатів. Захист від дублікатів спрацьовує лише пізніше
(при збереженні), але не попереджає про це на рівні списку вибору.

**Дві точки проблеми:**

**1.** Список кандидатів будується без перевірки індексу словника — слово
`enterprise` показується в списку хоча вже є в `wm-nouns-ua-en.xlsx` (рядок 793).

**2.** Кнопка «Додати всі рекомендовані слова» бере всіх кандидатів без фільтрації.
При режимі збереження `auto` слово-дублікат автоматично потрапило б у файл.

---

## Де в коді

**Корінь проблеми:**
`src/LagerthaAssistant.Api/Controllers/TelegramController.cs`
→ метод `TryHandleVocabularyImportFlowAsync` (~рядок 1894)

Після рядка:
```csharp
var orderedCandidates = OrderUrlCandidates(discovery.Candidates);
```
Немає виклику `_vocabularyIndexService.FindByInputsAsync`.

**Друга точка — «Додати всі»:**
→ метод `HandleVocabularyUrlSelectAllAsync` (~рядок 1870)
→ `session.Candidates` використовуються без фільтрації

**Сервіс для перевірки (вже ін'єктований):**
`IVocabularyIndexService` → `FindByInputsAsync(IReadOnlyList<string> inputs)`

---

## Рішення

**Крок 1.** Після `OrderUrlCandidates(...)` додати фільтрацію:

```csharp
var candidateWords = orderedCandidates.Select(c => c.Word).ToList();
var lookups = await _vocabularyIndexService.FindByInputsAsync(candidateWords, cancellationToken);
var newCandidates = orderedCandidates
    .Where(c => !lookups.TryGetValue(c.Word, out var lookup) || !lookup.Found)
    .ToList();
// Перенумерувати (1, 2, 3... без прогалин)
var renumbered = newCandidates
    .Select((c, i) => new PendingVocabularyUrlCandidate(i + 1, c.Word, c.PartOfSpeech, c.Frequency))
    .ToList();

// Якщо всі слова відомі
if (renumbered.Count == 0)
{
    _pendingStateStore.VocabularyUrlSessions.TryRemove(pendingKey, out _);
    return new TelegramRouteResponse(
        "vocab.url.all_known",
        _navigationPresenter.GetText("vocab.url.all_known", locale),
        InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
}
```

**Крок 2.** Додати ключ `vocab.url.all_known` в `LocalizationService.cs` (inline-словники):
- `English["vocab.url.all_known"]` = `"All suggested words are already in your vocabulary."`
- `Ukrainian["vocab.url.all_known"]` = `"Всі знайдені слова вже є у вашому словнику."`

**Крок 3.** У `HandleVocabularyUrlSelectAllAsync` — захисний фільтр:

```csharp
var lookups = await _vocabularyIndexService.FindByInputsAsync(selectedWords, cancellationToken);
selectedWords = selectedWords
    .Where(w => !lookups.TryGetValue(w, out var lookup) || !lookup.Found)
    .ToList();
```

---

## Тести (обов'язково)

```
1. 5 кандидатів, 2 є в індексі → показуються тільки 3, нумерація 1,2,3
2. 3 кандидати, всі в індексі → повертається "vocab.url.all_known"
3. 5 кандидатів, жодного в індексі → показуються всі 5 (без змін)
4. HandleVocabularyUrlSelectAllAsync: відомі слова не потрапляють у batch
5. Перенумерація коректна (без прогалин)
```

Фейки: `FakeVocabularyIndexService`, `FakeVocabularyDiscoveryService`

## Commit message
```
fix: filter known words from vocabulary url import candidate list (F02)
```


---
---
# F03 — Settings: 3-action dialog при закритті з незбереженими змінами

**Пріоритет:** 🟡 UX
**Тип:** Mini App UX / Frontend
**Залежності:** F01 (Settings відкривається — потім закривається)

---

## Проблема

Поточний UX закриття Settings з незбереженими змінами використовує нативний
Telegram popup що:
- не стилізований під Mini App
- має тільки 2 дії (Cancel / Close anyway)
- не дає варіанта «Зберегти зміни»
- на desktop виглядає як системний dialog, а не частина UI

---

## Де в коді

```
src/LagerthaAssistant.Web/src/pages/SettingsPage.tsx
  → hasUnsavedChanges logic
  → beforeunload listener
  → syncTelegramClosingConfirmation

src/LagerthaAssistant.Web/src/pages/settings-page-utils.ts
  → applyTelegramClosingConfirmation
  → syncTelegramClosingConfirmation
  → closeTelegramMiniApp
```

---

## Рішення

**Основний UX:** кастомний centered dialog всередині Mini App з трьома діями:
- «Зберегти зміни» (primary)
- «Закрити без збереження» (destructive)
- «Залишитись у налаштуваннях» (secondary)

Порядок кнопок (desktop):
```
[Залишитись] [Закрити без збереження] [Зберегти зміни]
```

**Технічне рішення:**
- Для контрольованих exit scenarios → кастомний in-app modal
- `Telegram.WebApp.showPopup(...)` якщо потрібен native Telegram-styled варіант
- `beforeunload` і built-in closing behavior → залишити як last-resort fallback

**Дії:**
- «Зберегти зміни» → викликає поточний `handleSaveAll()` → після success закриває
- «Закрити без збереження» → закриває без save, прибирає unsaved state protection
- «Залишитись» → закриває modal, залишає на сторінці

---

## Acceptance Criteria

- [ ] При виході з незбереженими змінами показується centered dialog з 3 діями
- [ ] «Зберегти зміни» зберігає і закриває Mini App
- [ ] «Закрити без збереження» закриває без save
- [ ] «Залишитись» лишає на сторінці
- [ ] Built-in Telegram closing behavior більше не є primary UX
- [ ] Поточний save flow через savebar button не зламаний
- [ ] На desktop Telegram виглядає нормально

## Тести
- Є незбережені зміни → confirm показується
- Save через confirm → save flow запускається → Mini App закривається
- Discard → не зберігає → закриває
- Stay → нічого не відбувається
- Немає змін → confirm не показується

## Commit message
```
feat: replace native closing confirmation with 3-action centered dialog (F03)
```


---
---
# F04 — Telegram navigation: input-only keyboard + rename "Назад"

**Пріоритет:** 🟡 UX
**Тип:** Navigation / Backend
**Залежності:** немає

---

## Проблема

У сценаріях де після кнопки бот очікує вільний текстовий ввід (наприклад
`Словник → Додати слово`), знизу продовжують показуватись всі кнопки секції.
Це збиває UX і суперечить очікуваному flow.

Також: кнопка «Головне меню» семантично не завжди означає "головне меню" —
вона часто означає "назад". Потрібне послідовне перейменування.

---

## Де в коді

```
src/LagerthaAssistant.Api/Services/TelegramNavigationPresenter.cs
  → keyboard builders для кожної секції

src/LagerthaAssistant.Api/Controllers/TelegramController.cs
  → де після кнопки "Додати слово" надсилається prompt для вводу

src/LagerthaAssistant.Application/Navigation/NavigationRouter.cs
src/LagerthaAssistant.Application/Constants/NavigationSections.cs
src/LagerthaAssistant.Api/Models/PendingTelegramModels.cs
  → PendingChatActionKind enum (VocabularyAdd, VocabularyBatch, VocabularyImport, InventorySearch, MealCreation, FoodPhotoLog, InventoryAdjustQuantity)
```

---

## Рішення

**Крок 1.** Знайти всі handler-и де після кнопки бот переходить у режим
очікування вільного тексту. Перевірити `PendingChatActionKind` в `PendingTelegramModels.cs`.

**Крок 2.** Для input-only scenarios повертати keyboard з однією кнопкою:
```
[← Назад]
```
Замість повного набору кнопок секції.

**Крок 3.** Послідовне перейменування в коді:
- Button text: `Головне меню` → `Назад` (де семантично це "back")
- Method names: `BuildMainMenuButton` → `BuildBackButton` (якщо є)
- Constants: перевірити `CallbackDataConstants.Nav.Main` — можливо треба додати `Nav.Back`
- Тестові assertions: оновити

**Важливо:** Не ламати реальну навігацію. Якщо кнопка реально повертає в root
main menu — назва залишається, але перевірити чи це правильна поведінка.

---

## Acceptance Criteria

- [ ] `Словник → Додати слово` → тільки одна кнопка «← Назад»
- [ ] «Назад» повертає у меню Словника (не в root)
- [ ] Однакове правило для всіх input-only сценаріїв
- [ ] Перейменування «Головне меню» → «Назад» консистентне в UI і коді
- [ ] Старі тести оновлені
- [ ] Нові тести покривають input-only keyboard behavior

## Commit message
```
fix: show only back button in input-only navigation scenarios + rename to Back (F04)
```


---
---
# F05 — Weekly menu: compact grouped view з іконками та категоріями

**Пріоритет:** 🔵 UX Enhancement
**Тип:** Backend / Food domain
**Залежності:** немає

---

## Проблема

`Переглянути меню` показує страви як суцільний текстовий блок:
- назва страви
- одразу під нею список інгредієнтів
- без категорій, без іконок

При великій кількості страв екран стає нечитабельним.

---

## Де в коді

```
src/LagerthaAssistant.Application/Services/Agents/FoodTrackingConversationAgent.cs
  → HandleWeeklyViewAsync (основний render)

src/LagerthaAssistant.Api/Controllers/TelegramController.cs
  → HandleWeeklyMenuTextAsync (ще один render path — ПЕРЕВІРИТИ ОБИДВА)

src/LagerthaAssistant.Domain/Entities/Meal.cs
  → немає Category, IconEmoji (потрібно додати + EF міграція)

src/LagerthaAssistant.Application/Models/Food/FoodModels.cs
  → MealDto без Category, IconEmoji

src/LagerthaAssistant.Application/Services/Food/FoodSyncService.cs
  → MapToMeal() не підтягує icon/category з Notion
```

---

## Рішення

**Крок 1.** Domain/data model:
- Додати `Category` і `IconEmoji` у `Meal` entity
- Додати в `MealDto`
- Потрібна EF міграція (існуючі міграції в `src/LagerthaAssistant.Infrastructure/Migrations/`, 11 міграцій)

**Крок 2.** Notion sync:
- У `MapToMeal()` / `UpdateMeal()` підтягувати icon з `NotionPage.IconEmoji`
- Підтягувати Category з відповідної Notion property

**Крок 3.** Новий формат рядка страви:
```
🍝 Spaghetti Bolognese — 520 kcal • 20 min
```
- Без інгредієнтів у list view
- Fallback emoji: `🍽` якщо іконки немає

**Крок 4.** Grouped list формат:
```
Меню страв (24):

🍳 Breakfast
• 🥞 Potato pancakes
• 🥚 Omelette

🍝 Lunch / Dinner
• 🍝 Spaghetti — 520 kcal
```

**Крок 5.** Оновити всі render paths (FoodTrackingConversationAgent + TelegramController).

---

## Acceptance Criteria

- [ ] List view більше не показує інгредієнти
- [ ] Страви згруповані по категоріях
- [ ] Іконки з Notion відображаються, якщо є
- [ ] Fallback emoji якщо іконки немає
- [ ] Category/icon живуть в моделі, не лише у formatter
- [ ] Всі render paths дають однаковий формат
- [ ] Покрито тестами

## Commit message
```
feat: compact grouped weekly menu view with icons and categories (F05)
```


---
---
# F06 — Media intent selection: вибір дії для фото та файлів

**Пріоритет:** 🔵 Feature
**Тип:** UX / Navigation / Backend
**Залежності:** F04 (navigation patterns)

---

## Проблема

Коли користувач надсилає фото або файл, бот намагається обробити його
в одному конкретному сценарії. Немає єдиного entry point що запитує
що саме зробити з media.

**Що вже є в коді (перевикористати):**
- Vocabulary import flow (photo/file)
- Inventory photo restock/consume flow
- Weekly menu food photo analysis
- `DetectSourceTypeFromInbound(...)` в TelegramController

---

## Рішення

**Архітектура:**
```
Inbound media → media classification → capability resolution
  → показати keyboard вибору → user вибирає → route до existing flow
```

**Нові компоненти:**
- `TelegramMediaKind` enum (Photo, ImageDocument, TextDocument, PdfDocument, Spreadsheet, Unknown)
- `TelegramMediaCapability` enum (VocabImport, InventoryRestock, InventoryConsume, FoodPhoto)
- `TelegramMediaIntentResolver` — повертає list capabilities для media kind
- Pending media state в `TelegramPendingStateStore`
- Нові callback constants: `media:vocab_import`, `media:inventory_restock`, тощо
- Keyboard builders у `TelegramNavigationPresenter`

**Ключові правила:**
- Document з image mime/extension → photo-like capabilities
- Unsupported file type → чітке повідомлення
- Після вибору → existing flow (без дублювання бізнес-логіки)
- Якщо вже є інший pending state → вирішити пріоритет

**Не додавати в TelegramController напряму** — винести classification
і capability resolution у окремі класи.

---

## Acceptance Criteria

- [ ] Фото → бот питає що зробити (список capabilities)
- [ ] Файл → бот питає, список залежить від типу
- [ ] Image document → photo-like capabilities
- [ ] Unsupported → зрозуміле повідомлення
- [ ] Після вибору → existing flow (не нова логіка)
- [ ] Достатньо чисто щоб додати нову capability пізніше без переписування
- [ ] Покрито тестами

## Commit message
```
feat: unified media intent selection flow for photo and file inputs (F06)
```


---
---
# F07 — Додати Gemini як AI провайдер

**Пріоритет:** 🔵 Feature
**Тип:** SharedBotKernel + Backend + Frontend
**Залежності:** немає (але великий change)

---

## Проблема

Система підтримує тільки OpenAI і Claude. Gemini не доданий.
Архітектура provider-specific логіки зараз дублюється і switch-based,
що ускладнює додавання нового провайдера.

---

## Де в коді (всі точки зміни)

```
src/LagerthaAssistant.Application/Constants/AiProviderConstants.cs
src/SharedBotKernel/Constants/AiProviderConstants.cs   ← дублювання!
src/SharedBotKernel/Infrastructure/AI/ResolvingAiChatClient.cs
src/SharedBotKernel/Extensions/KernelServiceExtensions.cs
src/LagerthaAssistant.Infrastructure/Services/AiRuntimeSettingsService.cs
src/LagerthaAssistant.Api/Controllers/PreferenceAiController.cs
src/LagerthaAssistant.Api/Services/MiniAppSettingsCommitService.cs
src/LagerthaAssistant.Web/src/pages/settings-page-presenter.ts
src/LagerthaAssistant.Web/src/pages/SettingsPage.tsx
```

---

## Рішення

**Крок 1.** Архітектурне прибирання (не skip):
- Прибрати дубльований `AiProviderConstants` — залишити тільки в `SharedBotKernel`
- Додати provider descriptor/registry у SharedBotKernel:
  - `Id`, `DisplayName`, `Aliases`, `DefaultModel`, `SupportedModels`, `IconToken`
- Зменшити switch-case expansion pattern

**Крок 2.** Gemini chat client:
- Додати `GeminiChatClient : IAiChatClient`
- `GeminiOptions` / constants
- DI реєстрація в `KernelServiceExtensions`

**Крок 3.** `ResolvingAiChatClient` — оновити routing на Gemini

**Крок 4.** `AiRuntimeSettingsService` — додати Gemini provider:
- `SupportedProviders`, `TryNormalizeProvider`, `GetSupportedModels`,
  `GetDefaultModel`, `GetEnvironmentApiKey`

**Крок 5.** API — оновити controllers/commit service щоб Gemini проходив

**Крок 6.** Mini App UI:
- Gemini у provider chooser
- Іконки провайдерів (OpenAI / Claude / Gemini)
- `formatProviderLabel()` + `getProviderIcon()` у presenter

---

## Acceptance Criteria

- [ ] Gemini доступний як provider у runtime
- [ ] Gemini models з'являються при виборі Gemini
- [ ] Settings save flow працює для Gemini
- [ ] API key flow для Gemini
- [ ] Provider icons у Mini App settings
- [ ] Дубльований `AiProviderConstants` прибраний
- [ ] OpenAI і Claude не зламані
- [ ] Тести: SharedBotKernel + IntegrationTests + Frontend

## Commit message
```
feat: add Gemini provider with centralized provider registry and icons (F07)
```
