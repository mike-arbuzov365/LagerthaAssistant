# BaguetteDesign — UX Copy Deck (Wave 1, UA)

> Статус: Active
> Мова за замовчуванням: Українська
> Scope: Settings + Dashboard + State messages

---

## 1. Загальні принципи copy

1. Всі user-facing тексти у Wave 1 — українською.
2. Тон: професійний, короткий, без жаргону.
3. Кнопки: дієслово в наказовій формі.
4. Помилки: причина + конкретна дія.

---

## 2. Settings — TMA/Web (UA copy)

### 2.1 Header

| Ключ | Текст |
|---|---|
| settings.title | Налаштування студії |
| settings.subtitle | Керуйте AI, інтеграціями та правилами роботи |
| settings.role.designer | Режим дизайнера |
| settings.role.client | Режим клієнта |

### 2.2 Profile & Studio

| Ключ | Текст |
|---|---|
| profile.section | Профіль і студія |
| profile.studio_name | Назва студії |
| profile.interface_language | Мова інтерфейсу |
| profile.interface_language.value_default | Українська |
| profile.timezone | Часовий пояс |

### 2.3 AI Assistant Hub

| Ключ | Текст |
|---|---|
| ai.section | AI-асистент |
| ai.model | Модель |
| ai.tone | Стиль відповіді |
| ai.tone.draft | Чернетка |
| ai.tone.standard | Стандарт |
| ai.tone.exploration | Дослідження |
| ai.creativity | Рівень креативності |
| ai.strictness | Ступінь строгості |

### 2.4 Integration Center

| Ключ | Текст |
|---|---|
| integrations.section | Інтеграції |
| integrations.notion | Notion |
| integrations.drive | Google Drive |
| integrations.calendar | Google Calendar |
| integrations.status.connected | Підключено |
| integrations.status.error | Помилка |
| integrations.status.not_configured | Не налаштовано |
| integrations.last_sync | Остання синхронізація |
| integrations.action.resync | Оновити синхронізацію |
| integrations.action.connect | Підключити |
| integrations.action.retry | Спробувати знову |

### 2.5 Project Defaults

| Ключ | Текст |
|---|---|
| defaults.section | Параметри проєктів |
| defaults.currency | Валюта |
| defaults.work_hours | Робочі години |
| defaults.brief_language | Мова брифу |

### 2.6 Notifications

| Ключ | Текст |
|---|---|
| notifications.section | Сповіщення |
| notifications.mode | Режим сповіщень |
| notifications.mode.critical_only | Лише критичні |
| notifications.mode.full_assist | Повна підтримка |
| notifications.silent_hours | Тихі години |
| notifications.new_lead | Новий лід |
| notifications.deadline | Нагадування про дедлайн |
| notifications.client_inactive | Клієнт не відповідає |

### 2.7 Privacy & Advanced

| Ключ | Текст |
|---|---|
| privacy.section | Дані та приватність |
| privacy.export | Експортувати дані |
| privacy.clear_context | Очистити контекст |
| advanced.section | Розширені налаштування |
| advanced.api_tokens | API-токени |
| advanced.collapsed_hint | Відкрийте, щоб змінити технічні параметри |

### 2.8 Buttons/CTA

| Ключ | Текст |
|---|---|
| cta.save | Зберегти зміни |
| cta.cancel | Скасувати |
| cta.close | Закрити |
| cta.back | Назад |

---

## 3. Dashboard — TMA/Web (UA copy)

### 3.1 Header + KPI

| Ключ | Текст |
|---|---|
| dashboard.title | Панель студії |
| dashboard.subtitle | Огляд активності та пріоритетів на сьогодні |
| dashboard.kpi.new_leads | Нові ліди |
| dashboard.kpi.active_projects | Активні проєкти |
| dashboard.kpi.deadlines | Найближчі дедлайни |
| dashboard.kpi.alerts | Потребують уваги |

### 3.2 Sections

| Ключ | Текст |
|---|---|
| dashboard.section.inbox | Останні звернення |
| dashboard.section.leads | Воронка лідів |
| dashboard.section.projects | Проєкти |
| dashboard.section.alerts | Попередження |
| dashboard.section.quick_actions | Швидкі дії |

### 3.3 Quick Actions

| Ключ | Текст |
|---|---|
| actions.create_proposal | Створити комерційну пропозицію |
| actions.request_materials | Запросити матеріали |
| actions.schedule_call | Запланувати дзвінок |
| actions.resync_integrations | Оновити інтеграції |

---

## 4. State Messages (UA)

### 4.1 Loading

| Ключ | Текст |
|---|---|
| state.loading.default | Завантажуємо дані… |
| state.loading.integrations | Перевіряємо стан інтеграцій… |

### 4.2 Error

| Ключ | Текст |
|---|---|
| state.error.generic | Сталася помилка. Спробуйте ще раз. |
| state.error.integrations | Не вдалося оновити інтеграції. |
| state.error.retry | Спробувати знову |

### 4.3 Offline

| Ключ | Текст |
|---|---|
| state.offline.banner | Немає підключення до мережі. Частина дій недоступна. |
| state.offline.cta | Перевірити з'єднання |

### 4.4 Empty

| Ключ | Текст |
|---|---|
| state.empty.inbox | Звернень поки немає. |
| state.empty.leads | Ліди ще не з'явилися. |
| state.empty.projects | Активних проєктів поки немає. |
| state.empty.cta | Почати з брифу |

### 4.5 Success

| Ключ | Текст |
|---|---|
| state.success.saved | Зміни збережено |
| state.success.synced | Синхронізацію виконано |

---

## 5. Вимоги до застосування

1. Тексти з цього файлу є єдиним джерелом істини для Wave 1.
2. Англомовні рядки у фінальному UI для Wave 1 не використовуються.
3. Якщо потрібна англійська локаль у майбутньому, додається окремим deck (не змінюючи default UA).

