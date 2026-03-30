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

### Issue #010: BaguetteDesign проєкти + BaguetteDbContext ✅

**Tasks:**
- [x] Створити 4 csproj: Domain / Application / Infrastructure / Api
- [x] Додати project references на SharedBotKernel
- [x] Створити `BaguetteDbContext : KernelDbContext`
- [x] Перша міграція: `InitialCreate` (8 kernel tables + TenantId)
- [x] `Program.cs` з health endpoint
- [x] Dockerfile для Railway

**AC:** `GET /health` → 200, таблиці в PostgreSQL створені

---

### Issue #011: RoleRouter — визначення ролі по user_id ✅

**Story:** Як бот, я маю визначати чи це дизайнер чи клієнт на основі Telegram user_id.

**Tasks:**
- [x] `IRoleRouter` інтерфейс
- [x] `RoleRouter : IRoleRouter` — порівнює `userId` з `ADMIN_USER_ID` з config
- [x] `UserRole` enum: `Designer | Client`
- [x] 4 unit тести: admin → Designer, client → Client, zero, negative

---

### Issue #012: /start handler — контекстне меню ✅

**Story:** Як користувач, після /start я маю бачити меню відповідно до своєї ролі.

**Tasks:**
- [x] `StartCommandHandler` — перевіряє роль → відправляє відповідне меню
- [x] Клієнтське меню: 6 кнопок (бриф / прайс / портфоліо / питання / зв'язатися / статус)
- [x] Дизайнерське меню: 5 кнопок (inbox / ліди / проєкти / швидка дія / налаштування)
- [x] Автовизначення мови з `message.From.LanguageCode` → uk / en (fallback → en)
- [x] 4 unit тести: designer menu, client menu, en locale, null locale

**AC:** Клієнт і дизайнер бачать різні меню; 12/12 тестів зелені

---

### Issue #013: QuestionHandler — AI відповіді на питання ✅

**Story:** Як клієнт, я хочу поставити довільне питання і отримати відповідь від Claude.

**Tasks:**
- [x] `QuestionHandler` — client free-text → Claude (with system prompt about Baguette Design)
- [x] Завантажує останні 20 `ConversationHistoryEntries` → передає в Claude як context
- [x] Зберігає user + assistant entries в `ConversationHistoryEntries`
- [x] Після відповіді — Inline keyboard: [Бриф] [Прайс] [Портфоліо] [Зв'язатися]
- [x] `IConversationRepository` + `ConversationRepository` (find-or-create session)
- [x] `ClaudeChatClientAdapter` — bridges `ClaudeChatClient` to `IAiChatClient`
- [x] 4 unit тести: reply sent, entries saved, history passed to AI, session found by userId

---

### Issue #014: BriefFlowService — покроковий адаптивний бриф ✅

**Story:** Як клієнт, я хочу пройти бриф як живий діалог, де питання адаптуються під тип послуги.

**Tasks:**
- [x] `BriefFlowState` immutable record persisted as JSON in `UserMemoryEntries` (Key="brief_state")
- [x] Flow steps: ServiceType → Brand → Audience → Style → Deadline → Budget → Country → Summary → Completed
- [x] Navigation: skip / back / cancel via inline keyboard callbacks
- [x] Final step: Claude generates summary → client confirms → Lead saved to DB
- [x] `BriefValidator`: completeness_score, missing required fields (service_type, budget, deadline)
- [x] `Lead` entity + `LeadStatus` enum; `leads` table migration
- [x] `IUserMemoryRepository` + `UserMemoryRepository` (soft delete, find-or-create)
- [x] `ILeadRepository` + `LeadRepository`
- [x] TelegramController: handles `callback_query` for brief_* + brief_svc_* callbacks
- [x] 17 unit tests: BriefValidatorTests (7) + BriefFlowStateTests (10)

---

### Issue #015: PriceService — прайс з Notion ✅

**Story:** Як клієнт, я хочу переглянути прайс за категоріями.

**Tasks:**
- [x] `NotionPriceClient` — paginated query Notion DB → maps title/select/number → `PriceItem`
- [x] `NotionPriceOptions` — configurable property names (Name, Category, Description, Price, Currency, Country)
- [x] `PriceService.GetCategoriesAsync()` → distinct categories from DB cache; triggers sync if empty
- [x] `PriceService.GetByCategoryAsync(category)` → items for category; triggers sync if empty
- [x] `PriceRepository.UpsertAsync()` — upsert + soft-deactivate removed items
- [x] `PriceHandler` — `ShowCategoriesAsync` (inline keyboard per category), `ShowCategoryItemsAsync` (formatted list + brief CTA)
- [x] `PriceItem` entity + `price_items` table migration (unique index on NotionPageId)
- [x] TelegramController: handles "price" and "price_cat_*" callbacks
- [x] 5 unit tests: categories present/empty, items present/empty, en locale

---

### Issue #016: PortfolioService — портфоліо з Notion ✅

**Story:** Як клієнт, я хочу переглянути приклади робіт за категоріями.

**Tasks:**
- [x] `NotionPortfolioClient` — paginated query, maps title/select/rich_text, extracts cover URL (external/file)
- [x] `NotionPortfolioOptions` — configurable property names
- [x] `PortfolioService.GetCategoriesAsync / GetByCategoryAsync` — DB cache with Notion sync on empty
- [x] `PortfolioRepository.UpsertAsync` — upsert + soft-deactivate removed cases
- [x] `PortfolioHandler` — categories keyboard + one message per case with "🎯 Хочу схожий" button
- [x] `BriefFlowService.StartWithStyleAsync` — starts brief with pre-filled Style from portfolio case title
- [x] `PortfolioCase` entity + `portfolio_cases` table migration
- [x] TelegramController: handles "portfolio", "portfolio_cat_*", "portfolio_similar_*" callbacks
- [x] 6 unit tests: categories/cases present/empty, locale, description rendering

---

### Issue #017: ContactHandler + CalendarService ✅

**Story:** Як клієнт, я хочу записатись на дзвінок або надіслати повідомлення дизайнеру.

**Tasks:**
- [x] `ContactHandler` — 3 варіанти: message / brief / call
- [x] `ICalendarService.GetAvailableSlotsAsync()` → Google Calendar free/busy (slots 09:00–18:00)
- [x] Клієнт обирає слот → `CalendarService.BookSlotAsync()` → Google Calendar event + Meet link
- [x] `DesignerNotifier` — надсилає Telegram notification на `AdminUserId` (message + booking)
- [x] `CalendarEvent` entity + `calendar_events` table migration
- [x] `Notification` entity + `notifications` table migration; `NotificationTrigger` enum
- [x] Нагадування клієнту за 24h і 1h (через `Notifications` table, IsSent flag)
- [x] `GoogleCalendarService` — raw HTTP + JWT service account (без Google.Apis SDK)
- [x] `GoogleTokenProvider` — cached JWT token, RSA.ImportFromPem
- [x] `GoogleCalendarOptions`, `NotificationTrigger`, `CalendarSlot` (FormatUk/FormatEn)
- [x] TelegramController: "contact", "contact_message", "contact_call", "contact_slot_*" callbacks
- [x] Awaiting-message flag keyed by chatId; checked before brief flow in controller
- [x] 6 unit tests: ShowOptions, PromptForMessage, HandleSendMessage, CalendarSlots (available/none), en locale

**AC:** 46/46 тестів зелені

---

### Issue #018: StatusHandler ✅

**Story:** Як клієнт, я хочу бачити статус мого запиту або проєкту.

**Tasks:**
- [x] `IStatusHandler` + `StatusHandler` — знаходить останній Lead по userId
- [x] Людські статуси: New / InProgress / WaitingMaterials / Converted / Closed
- [x] WaitingMaterials → показує список відсутніх полів (Brand, Audience, Style, Deadline, Budget)
- [x] Якщо всі поля заповнені → "зв'яжіться для уточнення деталей"
- [x] AiSummary з брифу виводиться в повідомленні статусу
- [x] Якщо лід відсутній → CTA "Заповнити бриф"
- [x] `ILeadRepository.GetLatestByUserIdAsync` — OrderByDescending(CreatedAtUtc)
- [x] TelegramController: "status" callback → ShowStatusAsync
- [x] 7 unit tests (StatusHandlerTests); 53/53 тестів зелені

**AC:** 53/53 тестів зелені

---

## M2: Designer Tools (Тижні 6–7)

### Issue #020: InboxService + hybrid reply mode ✅

**Story:** Як дизайнер, я хочу бачити всі звернення зі статусами і відповідати через AI-чернетку.

**Tasks:**
- [x] `InboxHandler.ShowDialogsAsync` — список клієнтських сесій з emoji-статусами
- [x] `InboxHandler.OpenDialogAsync` — остання переписка (10 повідомлень) + поля Lead + AI-чернетка
- [x] Гібридний режим: Claude генерує чернетку → [✅ Надіслати][❌ Відхилити][✏️ Ручний режим]
- [x] Ручний режим (`inbox_manual_{clientId}`): бот пересилає кожне повідомлення дизайнера клієнту
- [x] Статуси: New/InProgress/Waiting/Closed — кнопки в картці діалогу, `ChangeDialogStatusAsync`
- [x] `DialogState` entity + migration `AddDialogStates`; `IDialogStateRepository` + `DialogStateRepository`
- [x] `IConversationRepository.FindSessionAsync + GetAllClientSessionsAsync` — нові методи
- [x] TelegramController: inbox, inbox_open_*, inbox_send_*, inbox_dismiss_*, inbox_manual_*, inbox_auto_*, inbox_status_*_* callbacks
- [x] Designer text routing: manual mode → `HandleDesignerManualMessageAsync`
- [x] 10 unit tests (InboxHandlerTests); 63/63 тестів зелені

**AC:** 63/63 тестів зелені

---

### Issue #021: LeadService + картка ліда ✅

**Story:** Як дизайнер, я хочу бачити структуровані заявки і вести їх по воронці.

**Tasks:**
- [x] `ILeadService.GetLeadsAsync(status?)` → список з фільтром по статусу
- [x] `ILeadService.GetByIdAsync + ChangeStatusAsync` — CRUD-операції
- [x] `LeadHandler.ShowLeadsAsync` — список лідів з emoji-статусами та кнопками
- [x] `LeadHandler.ShowLeadCardAsync` — повна картка з усіма полями + кнопки статусу + "Відкрити діалог"
- [x] `LeadHandler.ChangeLeadStatusAsync` — міняє статус в БД і підтверджує дизайнеру
- [x] `ILeadRepository.GetByIdAsync + GetAllAsync(status?)` — нові методи
- [x] TelegramController: "leads", "lead_card_{id}", "lead_status_{id}_{status}" callbacks
- [x] 6 unit tests (LeadHandlerTests); 69/69 тестів зелені

**AC:** 69/69 тестів зелені

---

### Issue #022: ProjectService + авто-структура Google Drive ✅

**Story:** Як дизайнер, я хочу створити проєкт і автоматично отримати структуру папок у Drive.

**Tasks:**
- [x] `Project` entity: ClientUserId, LeadId, Title, ServiceType, Budget, Deadline, GoogleDriveFolderUrl, Status, RevisionCount, MaxRevisions
- [x] `Project.FromLead(lead)` — конвертація Lead → Project
- [x] `ProjectHandler.ConvertLeadToProjectAsync` — Lead → Project в БД, LeadStatus = Converted
- [x] `ProjectHandler.ShowProjectsAsync` — список проєктів з emoji-статусами
- [x] `ProjectHandler.ShowProjectCardAsync` — картка: всі поля + Drive посилання + кнопки статусу
- [x] `ProjectHandler.ChangeProjectStatusAsync` — зміна статусу + notification клієнту (Completed/Waiting)
- [x] `IProjectRepository` + `ProjectRepository`; migration `AddProjects`
- [x] TelegramController: projects, project_card_*, project_status_*, project_revision_*, lead_convert_* callbacks
- [x] 6 unit tests (ProjectHandlerTests); 78/78 тестів зелені

**Note:** Google Drive API інтеграція відкладена на M3 (потребує OAuth2 service account)

---

### Issue #023: RevisionCounter ✅

**Story:** Як дизайнер, я хочу рахувати кола правок і показувати клієнту залишок.

**Tasks:**
- [x] `Project.IsRevisionLimitReached` — computed property (`RevisionCount >= MaxRevisions`)
- [x] `ProjectHandler.AddRevisionAsync` — increment RevisionCount, status = InRevision
- [x] Клієнту після кожного кола: "Використано N з M кіл правок, залишилось K"
- [x] При досягненні ліміту → alert дизайнеру з червоним індикатором 🔴
- [x] Unit тести: `AddRevision_BelowLimit_IncrementsAndNotifiesClient`, `AddRevision_LimitReached_SendsAlert`, `Project_IsRevisionLimitReached_*`

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
