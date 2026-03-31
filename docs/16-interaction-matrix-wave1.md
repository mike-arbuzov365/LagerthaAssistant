# BaguetteDesign — Interaction Matrix (Wave 1)

> Статус: Active
> Scope: `QUEUE-017`
> Екрани: Settings + Dashboard (TMA + Web)
> Мова UX: українська

---

## 1. Глобальні правила переходів станів

| Trigger | Before | During | Success | Error | Offline |
|---|---|---|---|---|---|
| Save settings | idle | loading + disabled submit | toast `Зміни збережено` | inline error + `Спробувати знову` | banner + блокування submit |
| Re-sync integration | idle | loading on row/badge | status `Підключено` + `Оновлено щойно` | status `Помилка` + retry CTA | status `Офлайн` + retry disabled |
| Open dashboard block | idle | skeleton | data visible | error card | offline cached stub |
| Run quick action | idle | loading on action | success toast + row update | action error + retry | action blocked |

Rules:
1. `loading` завжди має видимий індикатор у зоні дії.
2. `error` завжди має retry/fallback CTA.
3. `offline` блокує destructive/submit та показує причину блокування.

---

## 2. Settings — Interaction Matrix

| Interaction | Precondition | Response (UI) | Success copy | Error copy | Offline copy |
|---|---|---|---|---|---|
| Змінити мову інтерфейсу | Settings loaded | відкриття selector | `Мову змінено` | `Не вдалося змінити мову` | `Немає мережі. Зміна мови недоступна` |
| Перемкнути режим сповіщень | Notifications section visible | toggle loading 300-500ms | `Налаштування оновлено` | `Не вдалося оновити режим` | `Немає мережі. Спробуйте пізніше` |
| Змінити робочі години | field editable | field saving state | `Робочі години збережено` | `Перевірте формат часу` | `Збереження недоступне офлайн` |
| Re-sync Notion/Drive/Calendar | integration row visible | row-level loading | `Синхронізацію виконано` | `Не вдалося виконати синхронізацію` | `Немає мережі для синхронізації` |
| Export data | user confirms | button loading | `Експорт підготовлено` | `Експорт тимчасово недоступний` | `Експорт недоступний офлайн` |
| Clear context | confirm modal accepted | destructive loading | `Контекст очищено` | `Не вдалося очистити контекст` | `Дія недоступна без мережі` |

---

## 3. Dashboard — Interaction Matrix

| Interaction | Precondition | Response (UI) | Success copy | Error copy | Offline copy |
|---|---|---|---|---|---|
| Відкрити Inbox Snapshot item | items exist | open details state | `Діалог відкрито` | `Не вдалося відкрити діалог` | `Доступний лише кешований перегляд` |
| Змінити статус ліда | lead row visible | status loading | `Статус ліда оновлено` | `Не вдалося оновити статус` | `Зміна статусу недоступна офлайн` |
| Додати правку до проєкту | project row visible | row updating | `Правку зафіксовано` | `Не вдалося додати правку` | `Офлайн: синхронізація після підключення` |
| Натиснути Quick Action | section visible | action loading | `Дію виконано` | `Не вдалося виконати дію` | `Дія тимчасово недоступна офлайн` |
| Retry alert | alert in error state | retry loading | `Спробу виконано` | `Повтор не вдався` | `Немає мережі для повтору` |

---

## 4. Mock Data Pack A — Normal Day

### 4.1 KPI

1. Нові ліди: `4`
2. Активні проєкти: `7`
3. Найближчі дедлайни: `3`
4. Потребують уваги: `2`

### 4.2 Sample rows (UA)

1. Inbox: `Олена / Бриф логотипу / 5 хв тому`
2. Leads: `Кав'ярня Ранок / переговори / бюджет $300`
3. Projects: `Nova Brand / в роботі / правки 1/2`
4. Alerts: `Не відповідає 3 дні / Відкрити`

---

## 5. Mock Data Pack B — Heavy Day

### 5.1 KPI

1. Нові ліди: `12`
2. Активні проєкти: `15`
3. Найближчі дедлайни: `6`
4. Потребують уваги: `7`

### 5.2 UX focus

1. Перевірка читабельності при великій щільності даних.
2. Перевірка truncation довгих назв.
3. Перевірка швидких дій у переповненому стані.

---

## 6. Mock Data Pack C — Incident Day

### 6.1 KPI

1. Нові ліди: `3`
2. Активні проєкти: `8`
3. Найближчі дедлайни: `4`
4. Потребують уваги: `9`

### 6.2 Incident profile

1. `Google Calendar` — `Помилка`
2. `Notion` — `Офлайн`
3. Частина дій у Quick Actions заблокована
4. На екрані є банер: `Немає підключення до мережі`

---

## 7. Acceptance Criteria для `QUEUE-017`

1. Для кожної ключової дії Settings і Dashboard є опис `success/error/offline`.
2. Є 3 mock data packs: `Normal / Heavy / Incident`.
3. Усі тексти у матрицях і моках українською.
4. Матриця узгоджена з:
- `docs/12-wave1-screen-blueprint.md`
- `docs/14-ux-copy-wave1-ua.md`
- `docs/figma-queue.md` (`QUEUE-017`)
