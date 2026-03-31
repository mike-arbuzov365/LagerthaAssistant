# BaguetteDesign — Figma MCP Workflow

> Версія: 1.0 | Статус: Активна
> Ліміт: **6 підключень до Figma через MCP на місяць**

---

## Філософія: "Накопичуй офлайн — застосовуй одним підключенням"

Figma MCP — це дорогий ресурс. Кожне підключення потрібно використовувати максимально ефективно. Тому весь процес розбитий на дві фази: **офлайн-підготовка** (без підключення) та **батч-застосування** (одне підключення = максимум змін).

### Hard Gate: Апрув власника

Навіть якщо в `figma-queue.md` накопичено `>= 5` значущих змін або досягнуто milestone, підключення до Figma MCP заборонене без явного апруву власника.

---

## Два режими роботи

### Режим A: Офлайн (основний, без MCP)

Codex або Claude Code **не підключається до Figma**, а:
- Описує компоненти текстом і JSON-специфікаціями
- Генерує React/JSX код компонентів
- Пише зміни у файл-черги (`figma-queue.md`)
- Готує Figma Plugin code або Figma REST API payload
- Оновлює дизайн-токени у CSS/JSON форматі

### Режим B: Онлайн (рідко, лише для застосування)

Codex **підключається до Figma через MCP** тільки коли:
1. Є явний апрув власника на підключення
2. Черга офлайн-змін достатньо велика (>5 значущих змін)
3. Потрібна точна перевірка реалізованого екрана
4. Фінальний pass перед здачею milestone

---

## Файл черги: figma-queue.md

Кожна офлайн-зміна ОБОВ'ЯЗКОВО записується у файл `docs/figma-queue.md`.

### Формат запису

```markdown
## Черга змін для Figma (незастосовані)
Останнє застосування: [дата]
Наступне заплановане застосування: [дата або milestone]

---

### QUEUE-001 | [назва зміни] | Пріоритет: High
**Тип:** New Component / Update / Delete / Style change
**Компонент/екран:** Button / Settings Screen / Color token
**Опис:**
Що саме потрібно змінити або створити в Figma.

**Специфікація:**
(точні розміри, кольори, поведінка)

**Reference code:**
```jsx
// Готовий JSX/код для перенесення в Figma
```

**Статус:** [ ] Pending / [x] Applied
---
```

### Приклад заповненого запису

```markdown
### QUEUE-003 | Integration Card Component | Пріоритет: High
**Тип:** New Component
**Компонент:** Integration Card (Atoms → Molecules)
**Опис:**
Картка для відображення стану інтеграції (Notion, Drive, Calendar).
Три стани: Підключено, Помилка, Не налаштовано.

**Специфікація:**
- Layout: горизонтальний flex, 68px висота
- Padding: 14px 16px
- Background: #1A1A1A, border-radius: 20px
- Зліва: логотип 32x32 (radius 8px)
- Центр: назва (15px/Medium/#FFFFFF) + "Остання синхронізація: 2 хв тому" (13px/Regular/#FFFFFF60)
- Справа: Status Badge + Chevron right icon

**Стани:**
- Підключено: badge bg rgba(52,199,89,0.15), text #34C759, "Підключено ●"
- Помилка: badge з пульсом, bg rgba(255,59,48,0.15), text #FF3B30, "Помилка ●"  
- Не налаштовано: badge bg rgba(255,255,255,0.08), text #FFFFFF40, "Не налаштовано ○"

**Reference code:**
```jsx
<IntegrationCard
  logo={<NotionLogo />}
  name="Notion"
  lastSync="2m ago"
  status="connected"   // 'connected' | 'error' | 'pending' | 'not-configured'
/>
```

**Статус:** [ ] Pending
---
```

---

## Структура Figma файлу

Дотримуватись цієї структури при кожному підключенні:

```
BaguetteDesign — Design System
│
├── 🎨 Foundations
│   ├── Colors          ← Figma Variables (усі CSS токени як Variables)
│   ├── Typography      ← Text Styles
│   ├── Spacing         ← Grid Styles
│   └── Icons           ← Icon set
│
├── ⚛️ Atoms
│   ├── Button          ← всі варіанти + стани
│   ├── Input Field     ← floating label + стани
│   ├── Toggle          ← on/off/loading/disabled
│   ├── Badge           ← всі семантичні варіанти
│   ├── Segmented Control
│   └── Icon Button
│
├── 🧩 Molecules
│   ├── Card            ← Default, Interactive, Status, Integration
│   ├── List Row        ← з іконкою, toggle, chevron
│   ├── Section Header  ← uppercase label
│   ├── Bottom Sheet    ← з handle + overlay
│   └── AI Slider       ← 3 позиції
│
├── 📱 Organisms
│   ├── Header TMA      ← з Role Switcher
│   ├── Settings Section ← повна секція налаштувань
│   └── Integration Hub ← 3 картки разом
│
└── 📄 Screens
    ├── Settings — TMA   ← повний мобільний екран
    ├── Settings — Web   ← десктоп адаптація
    ├── Dashboard        ← Designer Dashboard
    └── Brief Flow       ← клієнтський бриф
```

---

## Figma Variables (токени)

При підключенні — всі CSS токени мають бути перенесені як **Figma Variables**, не як захардкоджені значення. Це критично для масштабованості.

### Групи Variables у Figma

```
Colors/
  Background/primary     → #0F0F0F
  Background/surface     → #1A1A1A
  Background/elevated    → #242424
  Accent/gold            → #D4AF37
  Accent/gold-dim        → #B8942E
  Accent/ai              → #7C4DFF
  Accent/client          → #2D87FF
  Semantic/success       → #34C759
  Semantic/error         → #FF3B30
  Semantic/warning       → #FF9F0A
  Text/primary           → #FFFFFF
  Text/secondary         → rgba(255,255,255,0.60)
  Text/tertiary          → rgba(255,255,255,0.35)
  Border/default         → rgba(255,255,255,0.08)
  Border/focus           → rgba(212,175,55,0.60)

Spacing/
  1 → 4px
  2 → 8px
  3 → 12px
  4 → 16px
  6 → 24px
  8 → 32px
  12 → 48px

Radius/
  sm → 8px
  md → 12px
  lg → 16px
  xl → 20px
  full → 9999px
```

---

## Покроковий процес (один цикл)

### Фаза 1: Офлайн підготовка (Codex без MCP)

```
1. Отримати завдання на дизайн (нова фіча, компонент, екран)
2. Описати зміну в docs/figma-queue.md (формат QUEUE-NNN)
3. Написати JSX/React код компонента
4. Визначити точні токени (посилання на 08-design-system.md)
5. Описати всі стани (default, hover, active, disabled)
6. Додати до queue. Продовжити наступне завдання.
```

### Фаза 2: Батч-застосування (одне підключення MCP)

```
1. Перевірити figma-queue.md — скільки pending змін
2. Якщо є явний апрув власника І (`>= 5` значущих змін АБО milestone) → підключатись
3. При підключенні виконати ВСЮ чергу за одну сесію:
   a. Оновити/створити Figma Variables з 08-design-system.md
   b. Застосувати кожен QUEUE-NNN по черзі
   c. Перевірити auto-layout та constraints
   d. Перевірити стани (Component Properties)
   e. Зробити скріншот/preview кожного нового екрану
4. Позначити всі applied записи в queue: [x] Applied
5. Записати дату застосування в figma-queue.md
```

---

## Правила для Codex при офлайн-роботі

### Що МОЖНА робити без MCP

- Оновлювати `08-design-system.md` (токени, компоненти)
- Писати JSX-код компонентів
- Описувати специфікацію екранів текстом
- Додавати записи в `figma-queue.md`
- Оновлювати CSS Variables файл
- Генерувати Figma JSON payload (для batch import)

### Що НЕ МОЖНА без MCP

- Перевіряти чи правильно виглядає в Figma
- Отримувати актуальні node IDs
- Читати поточний стан Figma файлу
- Публікувати компоненти

### Формат Figma JSON для офлайн підготовки

Якщо потрібно підготувати зміни для batch import, Codex описує їх у форматі:

```json
{
  "type": "FRAME",
  "name": "Integration Card / Connected",
  "width": 343,
  "height": 68,
  "cornerRadius": 20,
  "fills": [{ "type": "SOLID", "color": { "r": 0.102, "g": 0.102, "b": 0.102 } }],
  "strokes": [{ "type": "SOLID", "color": { "r": 1, "g": 1, "b": 1, "a": 0.08 } }],
  "children": [...]
}
```

---

## Щомісячний план підключень (6 на місяць)

| Підключення | Коли | Що робимо |
|---|---|---|
| #1 | Початок місяця | Ініціалізація: Variables, Foundations, базові атоми |
| #2 | Після M0 накопичення | Atoms: Button, Input, Toggle, Badge |
| #3 | Після M1 накопичення | Molecules: Card, List Row, Integration Hub |
| #4 | Перший екран готовий | Settings Screen TMA + Web адаптація |
| #5 | Другий екран готовий | Dashboard + Brief Flow |
| #6 | Резервне | Виправлення, фінальний polish |

**Якщо підключення витрачено до кінця місяця** → продовжуємо офлайн, все в queue.

---

## Checklist перед кожним MCP підключенням

- [ ] `08-design-system.md` актуальний
- [ ] `figma-queue.md` містить всі pending зміни з описом
- [ ] Є явний апрув власника на це підключення
- [ ] Для кожного компонента є JSX reference code
- [ ] Всі стани описані (default, hover, active, error, disabled)
- [ ] Токени прив'язані до змінних (не захардкоджені hex)
- [ ] TMA Safe Areas враховані (100px bottom, 56px top)
- [ ] Анімації описані (duration, easing)
