# План розробки: BaguetteDesign V1

> Формат: GitHub Issues / Milestones
> Методологія: Вертикальні зрізи — щотижня working software
> Орієнтовна тривалість V1: 8 тижнів

---

## Milestones

| Milestone | Scope | Тижні |
|---|---|---|
| **M0: SharedBotKernel** | Передумова для всього | 1–2 |
| **M1: BaguetteDesign Core** | Клієнтський flow (бриф, прайс, портфоліо) | 3–5 |
| **M2: Designer Tools** | Inbox, ліди, проєкти, правки | 6–7 |
| **M3: Production Ready** | Polish, тести, деплой | 8 |

---

## Definition of Done (для кожного issue)

- [ ] Код написаний і проревьюваний (з AI)
- [ ] Unit тести для бізнес-логіки (якщо є domain logic)
- [ ] Протестовано вручну в Telegram (скріншот/відео)
- [ ] Задеплоєно на Railway staging
- [ ] Issue закрито, PR merged в `main`

---

## M0: SharedBotKernel (Тижні 1–2) ✅ ЗАВЕРШЕНО

> Реалізовано: березень 2026. Lagertha повністю функціональна — 826/826 тестів зелені.

### Issue #001: Створити BotPlatform.sln і SharedBotKernel.csproj ✅

**Tasks:**
- [x] Створити `BotPlatform.sln`
- [x] Створити `src/SharedBotKernel/SharedBotKernel.csproj` (Class Library, net10.0)
- [x] Додати всі існуючі Lagertha проєкти в новий sln
- [x] `dotnet build BotPlatform.sln` — зелено

---

### Issue #002: Перенести Domain entities → SharedBotKernel ✅

**Tasks:**
- [x] Перенести: `ConversationSession`, `ConversationHistoryEntry`, `UserMemoryEntry`, `SystemPromptEntry`, `ConversationIntentMetric`, `TelegramProcessedUpdate`, `BaseEntity`, `AuditableEntity`, `GraphAuthToken`, `UserAiCredential`
- [x] Namespace: `SharedBotKernel.Domain.*`
- [x] Global usings в Domain/Application/Infrastructure/Api/Tests для зворотної сумісності
- [x] Всі 826 Lagertha тестів зелені

---

### Issue #003: Витягти KernelDbContext (abstract) ✅

**Tasks:**
- [x] `SharedBotKernel/Persistence/KernelDbContext.cs` (abstract : DbContext)
- [x] 8 DbSet для спільних entities
- [x] Спільні EF configurations через `ApplyConfigurationsFromAssembly`
- [x] `AppDbContext : KernelDbContext`
- [x] Існуючі міграції без змін

---

### Issue #004: Перенести AI clients → SharedBotKernel

**Tasks:**
- [ ] Перенести: `IAiChatClient`, `ClaudeChatClient`, `OpenAiChatClient`, `ResolvingAiChatClient`, `AiSecretProtector`
- [ ] Оновити namespace
- [ ] Оновити DI реєстрацію в Lagertha

**AC:** Lagertha AI функціональність працює без змін

---

### Issue #005: Перенести Graph + Notion clients → SharedBotKernel

**Tasks:**
- [ ] Перенести: `GraphHttpClient`, `GraphAuthService`, `TokenManager`, `GraphAuthToken`
- [ ] Перенести: `NotionHttpClient` (базовий HTTP wrapper без доменної логіки)
- [ ] Перенести: `IClock`, `SystemClock`
- [ ] Оновити namespace і DI в Lagertha

---

### Issue #006: Перенести Telegram Adapter + витягти BackgroundSyncWorkerBase

**Tasks:**
- [ ] Перенести: `TelegramWebhookAdapter`, `TelegramDeduplicationService`
- [ ] Витягти `BackgroundSyncWorkerBase<TJob>` з `VocabularySyncHostedService` і `NotionSyncHostedService`
- [ ] Lagertha workers успадковують: `VocabularySyncWorker : BackgroundSyncWorkerBase<VocabularySyncJob>`

---

### Issue #007: Реалізувати AddKernelServices()

**Tasks:**
- [ ] Створити `SharedBotKernel/Extensions/KernelServiceExtensions.cs`
- [ ] `AddKernelServices()` реєструє: AI clients, NotionHttpClient, GraphHttpClient, IClock, TelegramAdapter
- [ ] Lagertha `Program.cs` використовує `AddKernelServices()` + власне
- [ ] Фінальний `dotnet test` — все зелене

**AC:** M0 завершено. Lagertha деплоїться на Railway без змін. SharedBotKernel готовий до підключення.

---

## M1: BaguetteDesign Core (Тижні 3–5)

### Issue #010: BaguetteDesign проєкти + BaguetteDbContext

**Tasks:**
- [ ] Створити 4 csproj: Domain / Application / Infrastructure / Api
- [ ] Додати project references на SharedBotKernel
- [ ] Створити `BaguetteDbContext : KernelDbContext`
- [ ] Додати 11 BaguetteDesign-specific DbSet
- [ ] EF конфігурації: indexes, constraints, jsonb columns
- [ ] Перша міграція: `dotnet ef migrations add InitialBaguetteCreate`
- [ ] `Program.cs` з `AddKernelServices()` + health endpoint
- [ ] Dockerfile для Railway

**AC:** `GET /health` → 200, таблиці в PostgreSQL створені

---

### Issue #011: RoleRouter — визначення ролі по user_id

**Story:** Як бот, я маю визначати чи це дизайнер чи клієнт на основі Telegram user_id.

**Tasks:**
- [ ] `IRoleRouter` інтерфейс
- [ ] `RoleRouter : IRoleRouter` — порівнює `userId` з `ADMIN_USER_ID` з config
- [ ] `UserRole` enum: `Designer | Client`
- [ ] Unit тести: `GivenAdminId_Returns_Designer`, `GivenOtherId_Returns_Client`

```csharp
// Test example
[Fact]
public void GivenAdminId_ReturnsDesigner()
{
    var router = new RoleRouter(adminUserId: "12345");
    Assert.Equal(UserRole.Designer, router.Resolve("12345"));
}
```

---

### Issue #012: /start handler — контекстне меню

**Story:** Як користувач, після /start я маю бачити меню відповідно до своєї ролі.

**Tasks:**
- [ ] `StartCommandHandler` — перевіряє роль → відправляє відповідне меню
- [ ] Клієнтське меню: 6 кнопок (бриф / прайс / портфоліо / питання / зв'язатися / статус)
- [ ] Дизайнерське меню: 5 кнопок (inbox / ліди / проєкти / швидка дія / налаштування)
- [ ] Автовизначення мови з `message.From.LanguageCode` → uk / en
- [ ] Збереження `UserMemoryEntry`: `{Key: "lang", Value: "uk"}`
- [ ] Реферальний лінк `?start=ref_xxx` → запис `tenant_id` клієнта

**AC:** Клієнт і дизайнер бачать різні меню; вручну протестовано в Telegram

---

### Issue #013: QuestionHandler — AI відповіді на питання

**Story:** Як клієнт, я хочу поставити довільне питання і отримати відповідь від Claude.

**Tasks:**
- [ ] `QuestionHandler` — якщо немає активного flow, відправляє в Claude
- [ ] Завантажує `ConversationHistoryEntries` для цього user → передає в Claude як context
- [ ] Зберігає відповідь у `ConversationHistoryEntries`
- [ ] Після відповіді — Inline keyboard з наступними кроками: [Бриф] [Прайс] [Портфоліо] [Зв'язатися]
- [ ] Якщо Claude не знає → пропонує зв'язатись з дизайнером

---

### Issue #014: BriefFlowService — покроковий адаптивний бриф

**Story:** Як клієнт, я хочу пройти бриф як живий діалог, де питання адаптуються під тип послуги.

**Tasks:**
- [ ] `BriefFlowState` — persisted в `UserMemoryEntries`: `{Key: "brief_state", Value: json}`
- [ ] Flow steps: service_type → brand → audience → style → references → deadline → budget → country
- [ ] Адаптація: якщо service_type=logo → розширені питання про бренд; social → формат і кількість
- [ ] Клієнт може: пропустити / повернутись / зупинити і продовжити
- [ ] Файли в діалозі → зберігаються в `FileRecords` з type=reference
- [ ] Фінал: Claude генерує summary → клієнт підтверджує → Lead автоматично в БД + Notion
- [ ] `AiAnalysis` брифу: completeness_score, missing_fields[]
- [ ] Unit тести: `BriefValidator` — перевірка повноти

---

### Issue #015: PriceService — прайс з Notion

**Story:** Як клієнт, я хочу переглянути прайс за категоріями.

**Tasks:**
- [ ] `NotionPriceClient.SyncPriceItemsAsync()` — завантажує з Notion DB → PostgreSQL cache
- [ ] `PriceService.GetCategoriesAsync()` → список категорій
- [ ] `PriceService.GetCategoryDetailsAsync(category, country)` → ціна для ринку
- [ ] `NotionSyncWorker : BackgroundSyncWorkerBase<PriceSyncJob>` — авто-оновлення кожні 60 хв
- [ ] Conversation flow: категорії → деталі → кнопка "Перейти до брифу"

---

### Issue #016: PortfolioService — портфоліо з Notion

**Story:** Як клієнт, я хочу переглянути приклади робіт за категоріями.

**Tasks:**
- [ ] `NotionPortfolioClient.SyncPortfolioCasesAsync()` → PostgreSQL cache
- [ ] Conversation flow: категорії → кейси → опис + кнопки "Хочу схожий" / "Бриф"
- [ ] "Хочу схожий" → запускає BriefFlow з pre-filled `style` field

---

### Issue #017: ContactHandler + CalendarService

**Story:** Як клієнт, я хочу записатись на дзвінок або надіслати повідомлення дизайнеру.

**Tasks:**
- [ ] `ContactHandler` — 3 варіанти: message / brief / call
- [ ] `CalendarService.GetAvailableSlotsAsync()` → Google Calendar free/busy
- [ ] Клієнт обирає слот → `CalendarService.BookSlotAsync()` → подія + Meet link
- [ ] Дизайнер отримує notification в Telegram: хто, коли, AI summary переписки
- [ ] `CalendarEvent` зберігається в БД
- [ ] Нагадування клієнту за 24h і 1h (через `Notifications` table)

---

### Issue #018: StatusHandler

**Story:** Як клієнт, я хочу бачити статус мого запиту або проєкту.

**Tasks:**
- [ ] `/status` command або кнопка
- [ ] Знаходить активний Lead або Project для цього client_id
- [ ] Показує людський статус (не технічний enum)
- [ ] Якщо "Чекаємо матеріали" → показує що саме потрібно

---

## M2: Designer Tools (Тижні 6–7)

### Issue #020: InboxService + hybrid reply mode

**Story:** Як дизайнер, я хочу бачити всі звернення зі статусами і відповідати через AI-чернетку.

**Tasks:**
- [ ] `InboxService.GetDialogsAsync(status)` → список чатів з фільтром
- [ ] При відкритті чату: повна переписка + `AiAssistantService.GenerateSummaryAsync()` + поля брифу
- [ ] Гібридний режим: Claude генерує чернетку відповіді → Inline buttons [Надіслати][Редагувати][Відхилити]
- [ ] Ручний режим: кнопка "Перейти в ручний режим" → бот пересилає все що пише дизайнер
- [ ] Внутрішні нотатки: зберігаються в Messages з role=`internal_note`
- [ ] Статуси чату: new → in_progress → waiting → closed

---

### Issue #021: LeadService + CartЬ ліда

**Story:** Як дизайнер, я хочу бачити структуровані заявки і вести їх по воронці.

**Tasks:**
- [ ] `LeadService.GetLeadsAsync(status?)` → список
- [ ] Картка ліда: всі поля + кнопки дій
- [ ] `LeadService.ChangeStatusAsync()` → оновити в БД і Notion
- [ ] `LeadService.ConvertToProjectAsync()` → створює Project з Lead

---

### Issue #022: ProjectService + авто-структура Google Drive

**Story:** Як дизайнер, я хочу створити проєкт і автоматично отримати структуру папок у Drive.

**Tasks:**
- [ ] `ProjectService.CreateProjectAsync()` → створює Project в БД
- [ ] `GoogleDriveClient.CreateProjectFolderAsync(clientName, projectTitle, year)` → папки: Source / Final / Revisions
- [ ] `ProjectService.ChangeStatusAsync()` → оновлює в БД, Notion і надсилає повідомлення клієнту
- [ ] Двостороння Notion синхронізація: зміна статусу в Notion → webhook → повідомлення клієнту

---

### Issue #023: RevisionCounter

**Story:** Як дизайнер, я хочу рахувати кола правок і показувати клієнту залишок.

**Tasks:**
- [ ] `ProjectService.AddRevisionAsync(projectId, description)` → increment `RevisionCount`
- [ ] `Project.IsRevisionLimitReached` — computed property
- [ ] Клієнту після кожного кола: "Використано N з M кіл правок"
- [ ] При перевищенні ліміту → notification дизайнеру
- [ ] Unit тест: `AddRevision_WhenLimitReached_SendsAlert`

---

### Issue #024: FileService — матеріали клієнтів

**Tasks:**
- [ ] `FileService.HandleIncomingFileAsync(update)` → завантажує файл з Telegram → Drive → записує FileRecord
- [ ] Авто-тегування за extension/MIME: .pdf/.docx → text; .png/.jpg → reference
- [ ] `FileService.RequestMaterialsAsync(clientId)` → шаблонне повідомлення клієнту

---

### Issue #025: CommercialProposalService (КП)

**Tasks:**
- [ ] `CommercialProposalService.GenerateDraftAsync(leadId)` → Claude генерує з даних брифу
- [ ] Дизайнер бачить чернетку → [Надіслати][Редагувати]
- [ ] Після надсилання → копія в Notion

---

### Issue #026: ReminderWorker + NotificationService

**Tasks:**
- [ ] `ReminderWorker : BackgroundSyncWorkerBase<Notification>`
- [ ] Тригери: client_no_reply_3_days, deadline_tomorrow, overdue_payment_7_days, weekly_digest
- [ ] Щопонеділка тижневий дайджест дизайнеру
- [ ] Нагадування клієнту за 24h і 1h до дзвінка (calendar_event trigger)
- [ ] Всі нагадування дизайнеру з підтвердженням [Нагадати][Пропустити]

---

## M3: Production Ready (Тиждень 8)

### Issue #030: Unit + Integration тести

**Tasks:**
- [ ] `SharedBotKernel.Tests` — тести для спільних сервісів
- [ ] `BaguetteDesign.Tests` — тести для domain logic:
  - `RoleRouterTests`
  - `BriefValidatorTests`
  - `RevisionCounterTests`
  - `PriceServiceTests`
- [ ] Integration тести з Testcontainers: BriefFlow end-to-end
- [ ] Мінімум 70% coverage на бізнес-логіці

---

### Issue #031: GitHub Actions CI/CD

**Tasks:**
- [ ] `.github/workflows/ci.yml` — на кожен PR: `dotnet build` + `dotnet test`
- [ ] `.github/workflows/deploy-baguette.yml` — на push в `main` при змінах у `src/bots/BaguetteDesign/` або `src/shared/`:
  - `dotnet publish`
  - Deploy до Railway
  - `GET /health` — verify
  - Telegram webhook registration
- [ ] Path filters: зміни в Lagertha не тригерять деплой Baguette і навпаки

---

### Issue #032: Railway деплой + Webhook реєстрація

**Tasks:**
- [ ] Railway service "baguette-design" з env vars
- [ ] Dockerfile перевірений
- [ ] `scripts/register-baguette-webhook.ps1`
- [ ] Перевірка: `GET /health` → `{"status":"healthy","db":"connected"}`
- [ ] Ручний тест: /start від клієнта і від дизайнера

---

### Issue #033: Локалізація uk/en

**Tasks:**
- [ ] Resource files: `Localization/uk.json`, `Localization/en.json`
- [ ] `ILocalizationService` → шукає по `UserMemoryEntry` lang → повертає текст
- [ ] Всі bot messages через localization (не hardcoded)
- [ ] Автовизначення при /start з `message.From.LanguageCode`

---

## Порядок реалізації (однолінійний)

```
M0: #001 → #002 → #003 → #004 → #005 → #006 → #007
M1: #010 → #011 → #012 → #013 → #014 → #015 → #016 → #017 → #018
M2: #020 → #021 → #022 → #023 → #024 → #025 → #026
M3: #030 → #031 → #032 → #033
```

---

## GitHub Labels

| Label | Колір | Призначення |
|---|---|---|
| `kernel` | Purple | Стосується SharedBotKernel |
| `baguette` | Coral | Тільки BaguetteDesign |
| `lagertha` | Teal | Тільки LagerthaAssistant |
| `client-flow` | Blue | Клієнтський сценарій |
| `designer-flow` | Green | Дизайнерський сценарій |
| `integration` | Amber | Notion / Drive / Calendar |
| `infra` | Gray | Деплой / CI / DB |
| `bug` | Red | Баг |

---

## Git Branch Strategy

```
main             ← production, завжди deployable
  └── feat/M0-shared-kernel
  └── feat/M1-brief-flow
  └── feat/M1-price-portfolio
  └── feat/M2-inbox
  └── feat/M2-projects
  └── fix/brief-state-persistence
```

Один PR = один issue. Merge тільки якщо: тести зелені + ручно протестовано в Telegram.
