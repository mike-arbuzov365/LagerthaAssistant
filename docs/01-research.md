# Дослідження: BaguetteDesign — Telegram-бот-асистент для графічного дизайнера

> Статус: Завершено | Дата: Березень 2026
> Рішення: **Розробляємо** — ніша не закрита жодним існуючим рішенням

---

## 1. Формулювання проблеми

**Core problem:** Фрілансер-дизайнер витрачає 2–3 години на день на рутинну комунікацію: відповіді на типові питання клієнтів, збір брифів, пересилання прайсу, нагадування, контроль правок. Це час, який не генерує дохід і не створює дизайн.

**Наслідки:**
- Клієнти чекають відповіді годинами (в нічний час — до наступного дня)
- Інформація розкидана по різних чатах без структури
- Бриф збирається в хаотичному форматі → дизайнер уточнює деталі вже в процесі роботи
- Правки не рахуються → scope creep без можливості обґрунтувати доплату
- Немає системи лідів → деякі звернення губляться

**Аудиторія V1:** Один дизайнер-фрілансер (або мала студія до 3 осіб). Орієнтир — дизайнер з потоком 5–15 запитів на місяць, переважно з України та EU.

---

## 2. Аналіз конкурентів

| Платформа | Ключові особливості | Ціна | Проблема для нас |
|---|---|---|---|
| **Agentive** | No-code flow builder, база знань | $39–$449/міс | Закрита екосистема, немає PostgreSQL, немає deep Notion/Drive інтеграції |
| **HubSpot Sales Hub** | Потужна CRM, кваліфікація лідів | $400+/міс | Надто дорого, не Telegram-native, надто складно для фрілансера |
| **ManyRequests** | Клієнтський портал, AI brief review | SaaS модель | Немає Telegram, окремий портал (клієнт має реєструватись) |
| **Chatfuel** | No-code Telegram бот | Free – $15/міс | Поверхневий, немає AI, немає кастомних інтеграцій |
| **Tidio** | Live chat + боти, багатомовність | $18–$69/міс | Не Telegram, фокус на сайти, а не месенджери |

**Висновок:** Жодне рішення не поєднує Telegram-native UX + AI-driven бриф + PostgreSQL persistence + Notion/Drive/Calendar інтеграції + CRM для дизайнера.

---

## 3. Аналіз існуючого коду (LagerthaAssistant)

Репо: https://github.com/mike-arbuzov365/LagerthaAssistant

У LagerthaAssistant вже реалізовані компоненти, які можна перевикористати:

| Компонент | Готовність | Статус |
|---|---|---|
| ConversationSessions + History | Повністю | → SharedBotKernel |
| UserMemoryEntries (Key/Value + Confidence) | Повністю | → SharedBotKernel |
| SystemPromptEntries + Proposals workflow | Повністю | → SharedBotKernel |
| TelegramProcessedUpdates (dedup) | Повністю | → SharedBotKernel |
| ConversationIntentMetrics | Повністю | → SharedBotKernel |
| ClaudeChatClient / OpenAiChatClient | Повністю | → SharedBotKernel |
| NotionHttpClient (HTTP wrapper) | Частково | → SharedBotKernel (базовий) |
| GraphHttpClient + TokenManager (OAuth) | Повністю | → SharedBotKernel |
| BackgroundSyncWorkerBase (retry + backoff) | Потрібно витягти | → SharedBotKernel |
| Telegram Webhook Adapter | Повністю | → SharedBotKernel |

**Специфічне тільки для Lagertha (не переноситься):**
- VocabularyCards / Tokens / SyncJobs (Excel pipeline)
- FoodItem, Meal, GroceryList (food tracking)
- StoreAlias, ItemAlias (нові)
- Консольний UI

---

## 4. Технічна feasibility

**Telegram Webhook:** ✅ Вже реалізовано в Lagertha, переноситься в Kernel
**Claude API:** ✅ ClaudeChatClient вже є в Infrastructure/AI
**Notion API:** ✅ NotionHttpClient базовий є, потрібно розширити методами для дизайн-домену
**Google Drive API:** ⚠️ Потрібно написати (немає в Lagertha — тільки OneDrive/Graph)
**Google Calendar API:** ⚠️ Потрібно написати
**PostgreSQL:** ✅ EF Core + Npgsql повністю налаштовано
**Railway deploy:** ✅ Відпрацьовано workflow деплою в Lagertha

---

## 5. Рішення

**Рішення: Розробляємо BaguetteDesign** як другий бот у тому самому monorepo (`BotPlatform.sln`), що підключає `SharedBotKernel` — нову спільну бібліотеку, витягнуту з Lagertha.

**Обґрунтування:**
- Унікальна ніша, не зайнята конкурентами
- 70% інфраструктурного коду вже написано в Lagertha
- Monorepo підхід: Notion API змінився → правимо в одному місці
- Перший реальний клієнт (дизайнер) вже визначений
