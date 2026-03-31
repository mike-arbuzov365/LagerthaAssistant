# BaguetteDesign — Runbook деплою на Railway

> Статус: Active
> Платформа: Railway
> Останнє оновлення: 2026-03-31

---

## Передумови

- [ ] Доступ до Railway проекту
- [ ] PostgreSQL сервіс на Railway запущений (або підготовлений окремий)
- [ ] Telegram Bot Token отримано через @BotFather
- [ ] Claude API Key є
- [ ] PR #88 (або поточний dev) змерджено в master

---

## Крок 1 — Переконатись що тести зелені

```bash
dotnet test LagerthaAssistant.sln
```

Очікувано: **945/945 Passed, 0 Failed**.

---

## Крок 2 — Змерджити PR в master

1. GitHub → PR → Approve → Merge
2. Переконатись що GitHub Actions CI (`ci.yml`) пройшов зелено

---

## Крок 3 — Підготувати БД

### 3.1 Локально (перший деплой)

```bash
# Переконатись що dotnet-ef встановлено
dotnet tool install --global dotnet-ef

# Застосувати всі 8 міграцій
dotnet ef database update \
  --project src/BaguetteDesign.Infrastructure \
  --startup-project src/BaguetteDesign.Api \
  --connection "Host=<railway-postgres-host>;Port=5432;Database=baguette_design;Username=postgres;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

### 3.2 Поточні міграції (8 штук)
1. `20260329143353_InitialCreate`
2. `20260329192025_AddLeads`
3. `20260329193141_AddPriceItems`
4. `20260329194120_AddPortfolioCases`
5. `20260329195238_AddCalendarAndNotifications`
6. `20260330053328_AddDialogStates`
7. `20260330054121_AddProjects`
8. `20260330054632_AddClientFiles`

---

## Крок 4 — Створити сервіс на Railway

1. Railway Dashboard → **New Service** → **GitHub Repo**
2. Вибрати репо `LagerthaAssistant`
3. Налаштування:
   - **Root directory**: `/` (залишити пустим — монорепо)
   - **Dockerfile path**: `src/BaguetteDesign.Api/Dockerfile`
   - **Service name**: `baguette-design`

---

## Крок 5 — Встановити Environment Variables на Railway

| Змінна | Значення | Обов'язково |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=...;Port=5432;Database=baguette_design;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true` | Так |
| `Telegram__Enabled` | `true` | Так |
| `Telegram__BotToken` | токен від @BotFather | Так |
| `Telegram__WebhookSecret` | будь-який рандомний рядок (UUID) | Так |
| `Claude__ApiKey` | ключ Anthropic | Так |
| `Claude__Model` | `claude-sonnet-4-6` | Так |
| `Baguette__AdminUserId` | твій Telegram user ID (число) | Так |
| `NotionPrice__ApiKey` | Notion integration token | Опціонально |
| `NotionPrice__DatabaseId` | ID бази Notion з тарифами | Опціонально |
| `NotionPortfolio__ApiKey` | Notion integration token | Опціонально |
| `NotionPortfolio__DatabaseId` | ID бази Notion з портфоліо | Опціонально |
| `GoogleCalendar__ServiceAccountJson` | JSON service account (рядком) | Опціонально |
| `GoogleCalendar__CalendarId` | ID Google Calendar | Опціонально |

> Де взяти `Baguette__AdminUserId`: надіслати /start боту @userinfobot або знайти в Telegram Desktop (Settings → Advanced → ... → Copy user ID).

---

## Крок 6 — Деплой

### Автоматичний (після налаштування CI/CD)
Push у master автоматично тригерить `deploy-baguette.yml` якщо змінились файли в `src/BaguetteDesign.*` або `src/SharedBotKernel`.

### Ручний (перший раз)
```bash
# Встановити Railway CLI
npm install -g @railway/cli

# Авторизуватись
railway login

# Задеплоїти
railway up --service baguette-design
```

---

## Крок 7 — Перевірити health

```bash
curl https://<railway-url>/health
```

Очікувана відповідь:
```json
{"status":"healthy","db":"connected"}
```

Якщо `db = "unavailable"` — перевір рядок підключення і що міграції застосовані.

---

## Крок 8 — Зареєструвати Telegram webhook

```bash
# Зареєструвати (замінити TOKEN, URL і SECRET)
curl -X POST "https://api.telegram.org/bot<TOKEN>/setWebhook" \
  -d "url=https://<railway-url>/api/telegram/webhook" \
  -d "secret_token=<Telegram__WebhookSecret з Railway>"

# Перевірити реєстрацію
curl "https://api.telegram.org/bot<TOKEN>/getWebhookInfo"
```

Очікуваний результат `getWebhookInfo`:
```json
{
  "ok": true,
  "result": {
    "url": "https://<railway-url>/api/telegram/webhook",
    "has_custom_certificate": false,
    "pending_update_count": 0
  }
}
```

---

## Крок 9 — Ручне тестування в Telegram

- [ ] `/start` як клієнт → отримати вітальне повідомлення
- [ ] `/start` як дизайнер (твій userId = `AdminUserId`) → отримати дизайнерське меню
- [ ] Надіслати текст → бот відповідає
- [ ] Перейти до прайсу (якщо Notion підключений)
- [ ] Перейти до портфоліо (якщо Notion підключений)
- [ ] Перевірити нагадування про зустрічі (якщо Google Calendar підключений)

---

## Налаштування GitHub Actions (одноразово)

Для автоматичного деплою потрібен `RAILWAY_TOKEN` у GitHub Secrets:

1. Railway Dashboard → Account Settings → Tokens → **New Token**
2. GitHub Repo → Settings → Secrets and variables → Actions → **New repository secret**
3. Ім'я: `RAILWAY_TOKEN`, значення: токен з Railway

---

## Troubleshooting

| Проблема | Причина | Рішення |
|---|---|---|
| `401 Unauthorized` на webhook | Відсутній або невірний `Telegram__WebhookSecret` | Перевір змінну на Railway та secret_token в реєстрації |
| `{"status":"healthy","db":"unavailable"}` | БД недоступна | Перевір `ConnectionStrings__DefaultConnection` |
| Бот не відповідає | webhook не зареєстрований | Виконати Крок 8 |
| `Telegram__Enabled=false` | Бот вимкнений | Встановити `true` на Railway |
| Docker build fail | Помилка компіляції | Запустити `dotnet build` локально |

---

## Rollback

Railway зберігає попередні деплої. У разі проблеми:

1. Railway Dashboard → Deployments
2. Знайти попередній успішний деплой
3. **Redeploy** → підтвердити
