# Figma Queue — Черга змін

> Останнє застосування: —
> Наступне заплановане: One-shot батч у новому акаунті (після явного апруву власника)
> Залишилось підключень у місяці: **6 / 6**

---

## Як використовувати цей файл

1. **Codex або Claude Code** додає новий запис QUEUE-NNN при кожній офлайн-зміні
2. **Ніколи** не підключатись до Figma MCP для одиничної зміни
3. **Підключатись** тільки після явного апруву власника (hard gate)
4. Після апруву: якщо >= 5 pending або milestone — виконати батч-застосування
5. Після застосування позначити `[x] Applied` і записати дату

> Важливо: умова `>= 5 pending` або milestone сама по собі НЕ є дозволом на підключення. Спочатку потрібен явний апрув власника.

---

## Шаблон апруву на MCP-підключення

Використовувати цю фразу без змін, щоб апрув був однозначним:

`АПРУВ MCP FIGMA: дозволяю одне підключення для батч-застосування QUEUE-[NNN...NNN] у файлі [file-key/url].`

Приклад:

`АПРУВ MCP FIGMA: дозволяю одне підключення для батч-застосування QUEUE-001...QUEUE-017 у файлі [file-key/url].`

---

## Журнал апрувів

| Дата | Апрув від | Обсяг (QUEUE) | Файл | Коментар |
|---|---|---|---|---|
| — | — | — | — | — |

---

## Статистика

| Статус | Кількість |
|---|---|
| Pending | 17 |
| Applied | 0 |
| Total | 17 |

---

## Pending зміни

---

### QUEUE-001 | Figma Variables — Design Tokens | Пріоритет: Critical

**Тип:** Initialize Variables
**Що:** Створити всі CSS-токени з `08-design-system.md` як Figma Variables

**Групи Variables:**

```
Colors/Background/primary     → #0F0F0F
Colors/Background/surface     → #1A1A1A
Colors/Background/elevated    → #242424
Colors/Background/input       → #1F1F1F
Colors/Accent/gold            → #D4AF37
Colors/Accent/gold-dim        → #B8942E
Colors/Accent/ai              → #7C4DFF
Colors/Accent/client          → #2D87FF
Colors/Semantic/success       → #34C759
Colors/Semantic/error         → #FF3B30
Colors/Semantic/warning       → #FF9F0A
Colors/Semantic/info          → #5AC8FA
Colors/Semantic/success-soft  → rgba(52,199,89,0.15)
Colors/Semantic/error-soft    → rgba(255,59,48,0.15)
Colors/Semantic/warning-soft  → rgba(255,159,10,0.15)
Colors/Semantic/info-soft     → rgba(90,200,250,0.15)
Colors/Semantic/neutral-soft  → rgba(255,255,255,0.08)
Colors/Text/primary           → #FFFFFF
Colors/Text/secondary         → rgba(255,255,255,0.60)
Colors/Text/tertiary          → rgba(255,255,255,0.35)
Colors/Text/inverse           → #0F0F0F
Colors/Border/default         → rgba(255,255,255,0.08)
Colors/Border/focus           → rgba(212,175,55,0.60)
Colors/Border/error           → rgba(255,59,48,0.60)

Spacing/1 → 4   Spacing/2 → 8   Spacing/3 → 12
Spacing/4 → 16  Spacing/6 → 24  Spacing/8 → 32  Spacing/12 → 48

Radius/sm → 8   Radius/md → 12  Radius/lg → 16
Radius/xl → 20  Radius/full → 9999
```

**Статус:** [ ] Pending

---

### QUEUE-002 | Text Styles | Пріоритет: Critical

**Тип:** Initialize Text Styles
**Що:** Створити Text Styles у Figma

```
Display/Bold:     Inter, 24px, Bold 700, lh 1.2
Title/Semibold:   Inter, 20px, Semibold 600, lh 1.2
Headline/Semibold: Inter, 17px, Semibold 600, lh 1.3
Body/Regular:     Inter, 15px, Regular 400, lh 1.5
Body/Medium:      Inter, 15px, Medium 500, lh 1.5
Label/Medium:     Inter, 13px, Medium 500, lh 1.4
Label/Bold:       Inter, 13px, Bold 700, lh 1.4, letter-spacing 0.5px
Caption/Regular:  Inter, 12px, Regular 400, lh 1.4
```

**Статус:** [ ] Pending

---

### QUEUE-003 | Button Component | Пріоритет: High

**Тип:** New Component (Atom)
**Що:** Створити Button компонент з усіма варіантами та станами

**Component Properties:**
```
variant:  primary | secondary | destructive | ghost
size:     large | medium | small
state:    default | hover | pressed | loading | disabled
hasIcon:  boolean (left icon optional)
```

**Розміри:**
```
Large:  height 52px, padding 24px, font 17px/Semibold, radius 8px
Medium: height 44px, padding 20px, font 15px/Medium, radius 8px
Small:  height 44px, padding 16px, font 13px/Medium, radius 8px
```

**Варіанти:**
```
Primary:     fill var(--color-accent-gold), text var(--color-text-inverse), shadow var(--shadow-button)
Secondary:   fill transparent, stroke var(--color-accent-gold), text var(--color-accent-gold)
Destructive: fill transparent, stroke var(--color-error), text var(--color-error)
Ghost:       fill var(--color-neutral-soft), text var(--color-text-secondary)
```

**Reference JSX:**
```jsx
<Button
  variant="primary"    // primary | secondary | destructive | ghost
  size="large"         // large | medium | small
  loading={false}
  disabled={false}
  icon={<IconPlus />}  // optional
>
  Надіслати
</Button>
```

**Статус:** [ ] Pending

---

### QUEUE-004 | File Structure — Wave 1 Pages | Пріоритет: Critical

**Тип:** Initialize File Structure
**Що:** Створити структуру сторінок у новому Figma-файлі для Wave 1.

**Специфікація:**
```
00 Foundations
01 Settings WF
02 Dashboard WF
```

**Правила:**
- Без додаткових сторінок на старті
- Імена сторінок фіксовані (для стабільного workflow)

**Статус:** [ ] Pending

---

### QUEUE-005 | Foundations Board — Tokens + States | Пріоритет: Critical

**Тип:** New Board
**Компонент/екран:** `00 Foundations` → `Wave 1 Foundations`
**Що:** Створити базовий каркас Foundations з двома блоками.

**Склад:**
1. `Foundation / Tokens`:
   - Spacing, Radius, Typography, Palette, Safe Area rules
2. `Foundation / States`:
   - Loading, Offline, Error, Saved
   - Короткі правила переходів станів

**AC:**
- Блоки існують у `00 Foundations`
- Є візуальна заготовка для state-бейджів
- Є явний опис Safe Area політики

**Статус:** [ ] Pending

---

### QUEUE-006 | Settings WF (TMA + Web) | Пріоритет: High

**Тип:** New Screen Wireframe
**Компонент/екран:** `01 Settings WF`
**Що:** Створити wireframe-каркаси `Settings / TMA` і `Settings / Web Adaptation`.

**IA (обов'язково):**
- Header (Studio Active + role context)
- AI Assistant Hub
- Integration Center (Notion / Drive / Calendar + статуси)
- Project Defaults
- Notifications
- Privacy & Data
- Advanced (collapsed)

**TMA вимоги:**
- Safe Area Top
- Safe Area Bottom Reserve (під Main Button)

**Web вимоги:**
- Sidebar + Content layout
- Та сама IA, без зміни пріоритетів контенту

**Статус:** [ ] Pending

---

### QUEUE-007 | Dashboard WF (TMA + Web) | Пріоритет: High

**Тип:** New Screen Wireframe
**Компонент/екран:** `02 Dashboard WF`
**Що:** Створити wireframe-каркаси `Dashboard / TMA` і `Dashboard / Web Adaptation`.

**IA (обов'язково):**
- Header
- Today Overview
- Inbox Snapshot
- Leads Pipeline
- Projects
- Alerts
- Quick Actions

**TMA вимоги:**
- Safe Area Top
- Safe Area Bottom Reserve

**Web вимоги:**
- KPI row
- Дворядна сітка контент-блоків

**Статус:** [ ] Pending

---

### QUEUE-008 | Wave 1 Visual Pass v1 | Пріоритет: High

**Тип:** Style Update
**Компонент/екран:** Foundations + Settings WF + Dashboard WF
**Що:** Перевести wireframe-каркаси у visual draft на основі `08-design-system.md`.

**Застосувати:**
- Темна база: `#0F0F0F` / `#1A1A1A`
- Акцент `Baguette Gold` для primary-дій
- Типографічна ієрархія (Title/Headline/Body/Caption)
- Card hierarchy (surface/elevated)
- Семантичні кольори state-індикаторів

**Не робити:**
- Надмірні візуальні ефекти
- Відхилення від token-first підходу

**Статус:** [ ] Pending

---

### QUEUE-009 | Wave 1 QA + Polish (TMA-first) | Пріоритет: High

**Тип:** QA / Fix Pass
**Компонент/екран:** Settings + Dashboard
**Що:** Фінальний контроль перед здачею Wave 1.

**Checklist:**
- Safe Areas дотримані (top + bottom reserve)
- Мінімальна висота touch-елементів >= 44px
- Немає інтерактивного тексту < 13px (`12px` тільки для non-interactive caption)
- Є стани Loading / Offline / Error
- Web адаптація не змінює IA TMA
- Naming у файлі узгоджений і стабільний

**Статус:** [ ] Pending

---

### QUEUE-010 | Core Molecules Set v1 | Пріоритет: High

**Тип:** New Components (Molecules)
**Що:** Створити перевикористовувані молекули для обох екранів.

**Склад:**
1. Integration Card (Підключено / Помилка / Не налаштовано)
2. KPI Card
3. Action Row
4. Alert Row
5. Section Header

**Вимоги:**
- Автолейаут коректний для TMA та Web composition
- Текст через style tokens
- Семантичні стани через color tokens

**Статус:** [ ] Pending

---

### QUEUE-011 | State Demo Board | Пріоритет: Medium

**Тип:** New Board
**Компонент/екран:** `00 Foundations`
**Що:** Додати демонстраційну зону станів компонентів для QA.

**Покрити:**
- Loading
- Error
- Offline
- Empty
- Disabled

**AC:**
- Є окремий блок `State Demo / Wave 1`
- Наочно видно різницю між станами на темній темі

**Статус:** [ ] Pending

---

### QUEUE-012 | Naming + Layer Hygiene Pass | Пріоритет: Medium

**Тип:** Cleanup
**Що:** Привести назви сторінок/фреймів/секцій до стабільного неймінгу.

**Convention:**
- `Screen / Variant`
- `Section / Name`
- `Component / Variant / State`

**AC:**
- Немає безіменних або випадкових назв (`Frame 123`)
- Ієрархія читабельна для handoff
- Відповідає `12-wave1-screen-blueprint.md`

**Статус:** [ ] Pending

---

### QUEUE-013 | Localization Baseline — Default Ukrainian | Пріоритет: Critical

**Тип:** Product Rule + Screen Update
**Компонент/екран:** Settings + глобальні тексти Wave 1
**Що:** Зафіксувати й застосувати мовну політику в дизайні.

**Вимоги:**
- У `Settings` поле мови інтерфейсу має дефолт `Українська`
- Всі user-facing тексти в Wave 1 (Settings + Dashboard) — українською
- Не використовувати англомовні CTA/лейбли у фінальних екранах Wave 1

**AC:**
- На екрані Settings видно дефолт `Українська`
- На Dashboard немає англомовних підписів/кнопок

**Статус:** [ ] Pending

---

### QUEUE-014 | UX Copy Deck — Settings (UA) | Пріоритет: High

**Тип:** Content Spec
**Компонент/екран:** Settings / TMA + Web
**Що:** Підготувати і застосувати український copy deck для всіх секцій Settings.
**Reference:** `docs/14-ux-copy-wave1-ua.md` (розділ 2)

**Покриття:**
- Header/title/subtitle
- Section headers
- Field labels
- Toggle labels
- Helper text
- CTA buttons
- Error/retry messages

**AC:**
- Для кожного елемента Settings є узгоджений український текст
- Тексти не змішані з англійськими варіантами

**Статус:** [ ] Pending

---

### QUEUE-015 | UX Copy Deck — Dashboard + States (UA) | Пріоритет: High

**Тип:** Content Spec
**Компонент/екран:** Dashboard / TMA + Web
**Що:** Підготувати і застосувати український copy deck для Dashboard і станів.
**Reference:** `docs/14-ux-copy-wave1-ua.md` (розділи 3-4)

**Покриття:**
- KPI labels
- Section names
- Action labels
- Alert messages
- Empty states
- Offline/Loading/Error state text

**AC:**
- Всі dashboard тексти українською
- Для кожного state є український message + CTA

**Статус:** [ ] Pending

---

### QUEUE-016 | Atoms Set v1 — Input/Toggle/Segmented/Badge | Пріоритет: High

**Тип:** New Components (Atoms)
**Що:** Додати базові atoms для масштабування після Wave 1.
**Reference:** `docs/15-component-specs-wave1.md`

**Склад:**
1. Input Field (default/focus/error/disabled)
2. Toggle (off/on/loading/disabled)
3. Segmented Control (2-3 опції, active/inactive)
4. Status Badge (success/error/warning/info/not-configured)

**Вимоги:**
- Тільки через токени з `08-design-system.md`
- Мінімальна висота інтерактивних елементів: >= 44px
- Лейбли/placeholder у демо — українською

**Статус:** [ ] Pending

---

### QUEUE-017 | Interaction Matrix + Mock Data Packs | Пріоритет: Medium

**Тип:** UX Spec + QA Support
**Компонент/екран:** Settings + Dashboard
**Що:** Підготувати офлайн interaction matrix і 3 пакети мок-даних для перевірки.
**Reference:** `docs/12-wave1-screen-blueprint.md` + `docs/14-ux-copy-wave1-ua.md` + `docs/16-interaction-matrix-wave1.md`

**Interaction matrix:**
- click/toggle/save/retry/error/offline transitions
- блокування дій в offline
- visual feedback для loading/success/error

**Mock data packs:**
1. Normal Day
2. Heavy Day
3. Incident Day (error/offline-heavy)

**Вимоги до copy:**
- Усі значення і статуси в моках — українською

**Статус:** [ ] Pending

---

## Applied зміни

*(порожньо — ще нічого не застосовано)*
