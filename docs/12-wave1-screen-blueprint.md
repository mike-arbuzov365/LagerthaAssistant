# BaguetteDesign — Wave 1 Screen Blueprint (Offline)

> Статус: Active
> Призначення: Детальна специфікація для one-shot переносу в новий Figma
> Scope: Settings + Dashboard (TMA first, Web adaptation)

---

## 1. Global Canvas Rules

1. TMA Frame: `390 x 844`
2. Web Frame: `1280 x 844`
3. TMA content horizontal padding: `16`
4. TMA top safe area reserve: `56 + safe inset`
5. TMA bottom action reserve: `100`
6. Мінімальна інтерактивна висота: `44`
7. Мінімальний текст: `13`
8. Дефолтна мова UI: `Українська`
9. Весь user-facing copy у Wave 1: `Українською`

---

## 2. Settings — TMA Blueprint

### 2.1 Frame Tree

```text
Settings / TMA
├── Safe Area Top
├── Header
├── Section: AI Assistant Hub
├── Section: Integration Center
├── Section: Project Defaults
├── Section: Notifications
├── Section: Privacy & Data
├── Section: Advanced (Collapsed)
└── Safe Area Bottom Reserve
```

### 2.2 Layout Rules

1. Section gap: `32`
2. Card internal padding: `16`
3. Row gap inside cards: `8..12`
4. Header sticky semantics (на рівні структури, навіть якщо без prototype)
5. Integration rows мають 3 стани: `Підключено / Помилка / Не налаштовано`

### 2.3 Content Rules

1. AI controls:
- Model selector: `Claude / GPT-4o`
- Tone selector: `Чернетка / Стандарт / Дослідження`
- Creativity control: `3-step slider`
2. Notifications:
- Mode: `Лише критичні / Повна підтримка`
- Silent hours row
3. Advanced:
- API/tokens не розкриті за замовчуванням
4. Localization:
- Language row значення за замовчуванням: `Українська`
- Тексти секцій і дій в UI — українською

---

## 3. Settings — Web Adaptation Blueprint

### 3.1 Frame Tree

```text
Settings / Web Adaptation
├── Sidebar
│   ├── AI-асистент
│   ├── Інтеграції
│   ├── Параметри проєктів
│   ├── Сповіщення
│   ├── Дані та приватність
│   └── Розширені
└── Content
    ├── Content Header
    ├── Row 1: AI + Integrations
    └── Row 2: Defaults/Notifications + Privacy/Advanced
```

### 3.2 Adaptation Rules

1. IA порядок той самий, що в TMA
2. Sidebar не додає нових сутностей, лише навігація по тих самих блоках
3. Card компоненти реюзаються 1:1 (змінюється тільки композиція)

---

## 4. Dashboard — TMA Blueprint

### 4.1 Frame Tree

```text
Dashboard / TMA
├── Safe Area Top
├── Header
├── Огляд дня
├── Останні звернення
├── Воронка лідів
├── Проєкти
├── Попередження
├── Швидкі дії
└── Safe Area Bottom Reserve
```

### 4.2 Content Contracts

1. Today Overview: 3 KPI мінімум
2. Inbox Snapshot: >= 3 rows sample
3. Projects: include revision hint `N/M` у мінімум одному рядку
4. Alerts: мінімум 1 item типу “Спробувати знову”
5. Quick Actions: 4 дії
6. Усі заголовки/лейбли/CTA у dashboard — українською

---

## 5. Dashboard — Web Adaptation Blueprint

### 5.1 Frame Tree

```text
Dashboard / Web Adaptation
├── Web Header
├── KPI Row (4 cards)
├── Grid Top (Звернення + Ліди)
└── Grid Bottom (Проєкти + Швидкі дії/Попередження)
```

### 5.2 Adaptation Rules

1. KPI cards у 1 ряд
2. Content blocks у 2 ряди по 2 колонки
3. Без втрати сутностей з TMA

### 5.3 Breakpoints (Web)

1. `>= 1280px`: базовий layout 4 KPI + дворядна сітка 2x2.
2. `1024..1279px`: KPI лишаються в 1 ряд, контент переходить у 1 колонку.
3. `768..1023px`: header компактний, KPI у 2 ряди по 2, блоки в 1 колонку.
4. `< 768px`: використовувати TMA-layout як fallback без втрати IA.

---

## 6. State Matrix (Wave 1)

| Area | Loading | Error | Offline | Empty |
|---|---|---|---|---|
| Integrations | yes | yes | yes | no |
| KPI | yes | yes | yes | yes |
| Inbox/Leads/Projects | yes | yes | yes | yes |
| Actions | yes | yes | yes (blocked) | n/a |

Rules:
1. Offline блокує destructive/submit дії
2. Error завжди має CTA `Спробувати знову` або fallback-дію
3. Empty не виглядає як bug: показує ясний next step

---

## 7. Definition of Done (Wave 1 Offline)

1. Є повний каркас 4 екранів: Settings TMA/Web, Dashboard TMA/Web
2. Є Foundations board (Tokens + States)
3. Є базові atom/molecule компоненти для повторного складання
4. Усі обов'язкові state-cases покриті
5. Документація узгоджена з `08/09/10/11/figma-queue`
6. Дефолтна мова у Settings = українська, copy на екранах Wave 1 = українська
