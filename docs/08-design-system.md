# BaguetteDesign — Дизайн-система та UX-специфікація

> Версія: 1.0 | Статус: Активна | Дата: Березень 2026
> Джерела: UX-дослідження Gemini (Senior Product Designer) + TMA best practices

---

## Зміст

1. [Дизайн-концепція та філософія](#1-дизайн-концепція)
2. [Дизайн-токени](#2-дизайн-токени)
3. [Типографіка](#3-типографіка)
4. [Кольорова палітра](#4-кольорова-палітра)
5. [Сітка та відступи](#5-сітка-та-відступи)
6. [Компоненти](#6-компоненти)
7. [Екрани (специфікації)](#7-екрани)
8. [Платформна адаптація](#8-платформна-адаптація)
9. [Стан компонентів (States)](#9-стани-компонентів)

---

## 1. Дизайн-концепція

### Обраний напрям: "Creative Studio Dashboard"

**Візуальний характер:** Витончений, темний, card-based layout. Антрацитовий фон, акцентний Baguette Gold, великі радіуси (20px). Кожна секція — окремий модуль.

**UX-філософія:** "Робочий простір майстра". Бот для дизайнера має виглядати як інструмент, яким дизайнер пишається, а не соромиться показати клієнту.

**Чому НЕ "Liquid Glass":** Ризик зникнення бренду в системних стилях Telegram. BaguetteDesign — це самостійна студія, не просто бот.

**Чому НЕ "Agentic Minimalism":** Продукт має технічні параметри (Notion, Drive), які потребують чіткості, а не абстракцій.

### Зони відповідальності платформ

| Дія | Де відбувається |
|---|---|
| Сповіщення, статуси, нові звернення | Telegram чат |
| Бриф, проєкти, портфоліо, дашборд | Telegram Mini App (TMA) |
| Глибокі налаштування, аналітика, звіти | Web (майбутнє) |
| Жива переписка, голос | Тільки чат |

### Принципи продукту

- **Design-system-first** — спочатку токени, потім екрани
- **TMA-first** — кожне рішення тестується на мобільному Telegram
- **Shared tokens** — ті самі змінні для TMA та Web
- **Autosave за замовчуванням** — окрім критичних полів (API keys)
- **Role-aware UI** — інтерфейс адаптується під роль (дизайнер/клієнт)
- **Owner approval gate** — навіть якщо в черзі `>= 5` значущих змін або досягнуто milestone, AI не підключається до Figma MCP без явного апруву власника
- **Language-first UA** — дефолтна мова в налаштуваннях: українська; весь UX-copy у Wave 1: українською

---

## 2. Дизайн-токени

Токени — це "конституція" дизайну. Кожна зміна токена автоматично поширюється на весь продукт.

### Формат (CSS змінні / Figma Variables)

```css
/* ===== КОЛЬОРИ — БАЗА ===== */
--color-bg-primary:       #0F0F0F;   /* Головний фон сторінки */
--color-bg-surface:       #1A1A1A;   /* Поверхня карток */
--color-bg-elevated:      #242424;   /* Підняті елементи (dropdown, modal) */
--color-bg-input:         #1F1F1F;   /* Фон інпутів */

/* ===== КОЛЬОРИ — АКЦЕНТИ ===== */
--color-accent-gold:      #D4AF37;   /* Baguette Gold — головний CTA */
--color-accent-gold-dim:  #B8942E;   /* Gold на hover/pressed */
--color-accent-ai:        #7C4DFF;   /* Фіолетовий — все що стосується AI */
--color-accent-ai-dim:    #6B3FE0;   /* AI на hover */
--color-accent-client:    #2D87FF;   /* Синій — режим клієнта */

/* ===== КОЛЬОРИ — СЕМАНТИКА ===== */
--color-success:          #34C759;   /* Підключено, активно, збережено */
--color-error:            #FF3B30;   /* Помилка, відхилено */
--color-warning:          #FF9F0A;   /* Увага, очікує */
--color-info:             #5AC8FA;   /* Інформація */
--color-success-soft:     rgba(52,199,89,0.15);   /* Фон success badge/chip */
--color-error-soft:       rgba(255,59,48,0.15);   /* Фон error badge/chip */
--color-warning-soft:     rgba(255,159,10,0.15);  /* Фон warning badge/chip */
--color-info-soft:        rgba(90,200,250,0.15);  /* Фон info badge/chip */
--color-neutral-soft:     rgba(255,255,255,0.08); /* Фон neutral/not-configured */

/* ===== ТЕКСТ ===== */
--color-text-primary:     #FFFFFF;   /* Заголовки, основний текст */
--color-text-secondary:   rgba(255,255,255,0.60);  /* Підписи, підказки */
--color-text-tertiary:    rgba(255,255,255,0.55);  /* Placeholder, disabled (min 4.5:1 on bg-primary) */
--color-text-inverse:     #0F0F0F;   /* Текст на золотому фоні */

/* ===== МЕЖІ ===== */
--color-border-default:   rgba(255,255,255,0.08);  /* Стандартна межа картки */
--color-border-focus:     rgba(212,175,55,0.60);   /* Фокус на інпуті */
--color-border-error:     rgba(255,59,48,0.60);    /* Помилка в інпуті */

/* ===== ТИПОГРАФІКА ===== */
--font-family:            'Inter', -apple-system, sans-serif;
--font-size-display:      24px;   /* Hero заголовки */
--font-size-title:        20px;   /* Назви екранів */
--font-size-headline:     17px;   /* Заголовки секцій */
--font-size-body:         15px;   /* Основний текст */
--font-size-label:        13px;   /* Лейбли, описи */
--font-size-caption:      12px;   /* Найдрібніший текст, підписи */

--font-weight-bold:       700;
--font-weight-semibold:   600;
--font-weight-medium:     500;
--font-weight-regular:    400;

--line-height-tight:      1.2;   /* Заголовки */
--line-height-normal:     1.5;   /* Основний текст */
--line-height-relaxed:    1.7;   /* Довгі параграфи */

/* ===== ВІДСТУПИ (8px grid) ===== */
--space-1:    4px;
--space-2:    8px;
--space-3:    12px;
--space-4:    16px;
--space-5:    20px;
--space-6:    24px;
--space-8:    32px;
--space-10:   40px;
--space-12:   48px;

/* ===== РАДІУСИ ===== */
--radius-sm:   8px;    /* Кнопки, інпути, badge */
--radius-md:   12px;   /* Невеликі картки, чіпи */
--radius-lg:   16px;   /* Середні картки */
--radius-xl:   20px;   /* Великі картки, модалі */
--radius-full: 9999px; /* Пілюлі, аватари */

/* ===== ТІНІ ===== */
--shadow-card:    0 2px 12px rgba(0,0,0,0.40);
--shadow-modal:   0 8px 32px rgba(0,0,0,0.60);
--shadow-button:  0 2px 8px rgba(212,175,55,0.25);

/* ===== АНІМАЦІЯ ===== */
--duration-fast:    150ms;
--duration-normal:  250ms;
--duration-slow:    400ms;
--easing-default:   cubic-bezier(0.4, 0, 0.2, 1);
--easing-spring:    cubic-bezier(0.34, 1.56, 0.64, 1);

/* ===== TMA SAFE AREAS ===== */
--tma-header-height:   56px;
--tma-bottom-btn:      80px;   /* Зарезервовано для Main Button */
--tma-safe-top:        env(safe-area-inset-top, 0px);
--tma-safe-bottom:     env(safe-area-inset-bottom, 16px);
--content-padding-bottom: 100px;  /* Завжди лишати місце під TMA Bottom Button */
```

---

## 3. Типографіка

### Ієрархія

| Рівень | Розмір | Вага | Використання |
|---|---|---|---|
| Display | 24px / Bold | 700 | Hero-заголовки, ключові числа |
| Title | 20px / Semibold | 600 | Назви екранів, назви модалів |
| Headline | 17px / Semibold | 600 | Заголовки секцій, назви карток |
| Body | 15px / Regular | 400 | Основний контент, описи |
| Label | 13px / Medium | 500 | Лейбли полів, badge, caption |
| Caption | 12px / Regular | 400 | Підписи, дати, вторинна мета |

### Правила

- Мінімальний розмір шрифту для інтерактивного/операційного тексту у TMA: **13px**
- `12px` допускається лише для вторинного non-interactive metadata (дати, технічні підписи)
- **iOS Dynamic Type:** компоненти мають підтримувати масштабування шрифтів від 1× до 1.35× без обрізання контенту; фіксовані висоти елементів замінюються на `min-height`
- Section titles завжди: `13px / Bold / All-caps / letter-spacing: 0.5px`
- Placeholder у інпутах: `15px / Regular / --color-text-tertiary`
- Посилання: `--color-accent-gold`, underline тільки на hover

---

## 4. Кольорова палітра

### Основна

```
#0F0F0F  ██  bg-primary     — майже чорний фон
#1A1A1A  ██  bg-surface     — поверхня карток
#242424  ██  bg-elevated    — підняті елементи
#1F1F1F  ██  bg-input       — фон інпутів
```

### Акцентна

```
#D4AF37  ██  accent-gold    — Baguette Gold, кнопки, активні стани
#B8942E  ██  accent-gold-dim — hover/pressed
#7C4DFF  ██  accent-ai      — AI-функції (фіолетовий)
#2D87FF  ██  accent-client  — режим клієнта (синій)
```

### Семантична

```
#34C759  ██  success        — зелений (connected, saved, done)
#FF3B30  ██  error          — червоний (error, rejected)
#FF9F0A  ██  warning        — помаранчевий (pending, attention)
#5AC8FA  ██  info           — блакитний (info, sync)
```

### Правило переключення теми ролей
- **Дизайнер** → акцент `--color-accent-gold` (`#D4AF37`)
- **Клієнт** → акцент `--color-accent-client` (`#2D87FF`)
- Переключення відбувається в Header компоненті

---

## 5. Сітка та відступи

### 8px Grid System

Всі відступи кратні 8px. Внутрішні відступи карток — 16px. Між картками — 24px.

```
Між секціями:         32px  (--space-8)
Між картками:         12px  (--space-3)
Внутрішній padding картки:  16px  (--space-4)
Між елементами в картці:    12px  (--space-3)
Padding сторінки (горизонт): 16px  (--space-4)
```

### TMA Safe Areas

```
Верхній відступ контенту:   56px + env(safe-area-inset-top)  [під хедер]
Нижній відступ контенту:   100px  [під TMA Main Button]
Горизонтальні поля:         16px
```

---

## 6. Компоненти

### 6.1 Button

**Варіанти:**

| Тип | Фон | Текст | Використання |
|---|---|---|---|
| Primary | `#D4AF37` | `#0F0F0F` | Головна дія на екрані |
| Secondary | `transparent` | `#D4AF37` | Другорядна дія |
| Destructive | `transparent` | `#FF3B30` | Видалення, скидання |
| Ghost | `rgba(255,255,255,0.05)` | `#FFFFFF 60%` | Третинна дія |
| TMA Main Button | Native Telegram | Native | Єдина основна дія в TMA |

**Розміри:**

| Розмір | Висота | Padding | Font |
|---|---|---|---|
| Large | 52px | 24px | 17px / Semibold |
| Medium | 44px | 20px | 15px / Medium |
| Small | 44px | 16px | 13px / Medium |

**Стани:** default → hover (10% lighter) → pressed (scale 0.97) → loading (spinner) → disabled (30% opacity)

**Radius:** `--radius-sm` (8px)

---

### 6.2 Card

Основний контейнер для груп налаштувань, лідів, проєктів.

```
Background:    --color-bg-surface (#1A1A1A)
Border:        1px solid --color-border-default
Border-radius: --radius-xl (20px)
Padding:       --space-4 (16px)
Shadow:        --shadow-card
```

**Варіанти:**

- **Default Card** — стандартний контейнер
- **Interactive Card** — з hover-ефектом (bg піднімається до `#242424`)
- **Status Card** — з лівою кольоровою смугою (4px) для індикації стану
- **Integration Card** — для Notion/Drive/Calendar: логотип + назва + статус badge + дата sync

**Integration Card специфікація:**
```
Layout: горизонтальний flex
Зліва:  Лого сервісу (32x32px, radius-sm)
Центр:  Назва (Body/Medium) + "Остання синхронізація: 2 хв тому" (Caption/secondary)
Справа: Status badge + chevron
Padding: 14px 16px
Height:  68px мінімум
```

**Integration Card — станова матриця:**

| Стан | Badge | Дія | Опис |
|---|---|---|---|
| Connected | `Підключено` (success) | Re-sync | Нормальна робота |
| Error | `Помилка` (error, pulse) | Retry | Синхронізація не вдалася |
| Not configured | `Не налаштовано` (neutral) | Connect | Початкове підключення |
| Loading | spinner на badge | — (disabled) | Синхронізація у процесі |
| Offline | `Офлайн` (warning) | Retry (disabled) | Немає мережі |
| Retry pending | `Повтор...` (warning) | Cancel | Автоматичний retry через N сек |

---

### 6.3 Toggle (Switch)

```
Візуальна висота треку:   32px
Інтерактивна зона:        >= 44px (через wrapper frame/padding)
Ширина:        52px
Thumb:         26px діаметр, білий
Track active:  --color-accent-gold
Track inactive: --color-border-default
Transition:    250ms ease
```

**Стани:** off → on → loading (spinner всередині thumb) → disabled

---

### 6.4 Input Field

```
Height:        52px
Background:    --color-bg-input
Border:        1px solid --color-border-default
Border-radius: --radius-sm (8px)
Padding:       0 16px
Font:          15px / Regular

States:
  default:  border --color-border-default
  focus:    border --color-border-focus, glow 0 0 0 3px rgba(212,175,55,0.15)
  error:    border --color-border-error
  filled:   label піднімається (floating label 12px/Caption)
```

**Floating Label:**
- Default position: вертикально центрований, 15px
- Active/filled: translate(-50% top), 12px/Caption/secondary
- Transition: 150ms ease

---

### 6.5 Status Badge

```
Border-radius: --radius-full
Padding:       4px 10px
Font:          13px / Medium
```

| Статус | Фон | Текст | Точка |
|---|---|---|---|
| Підключено | --color-success-soft | --color-success | ● зелена |
| Помилка | --color-error-soft | --color-error | ● червона (пульс) |
| Очікує | --color-warning-soft | --color-warning | ● помаранчева |
| Не налаштовано | --color-neutral-soft | --color-text-tertiary | ○ сіра |

**Error state** має CSS animation пульс:
```css
@keyframes pulse-error {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}
```

---

### 6.6 Segmented Control

Для вибору режиму AI, мови, типу відображення.

```
Height:        44px
Background:    rgba(255,255,255,0.05)
Border-radius: --radius-sm (8px)
Padding:       4px (навколо)

Active segment:
  Background:  --color-bg-elevated (#242424)
  Font:        15px / Semibold
  Shadow:      --shadow-card
  Transition:  150ms spring
```

---

### 6.7 Section Header (Atom)

Заголовок групи налаштувань або секції. Класифікація: **atom** (самостійний неінтерактивний елемент без внутрішніх компонентів).

```
Font:     13px / Bold / All-caps / letter-spacing: 0.5px
Color:    --color-text-secondary
Padding:  8px 0 8px 16px
```

---

### 6.8 List Row

Рядок у списку (для простих налаштувань без картки).

```
Height:        52px мінімум
Padding:       0 16px
Background:    --color-bg-surface
Border-bottom: 1px solid --color-border-default (крім останнього)

Layout:
  Зліва:  іконка 24x24 (optional) + label (Body/primary)
  Справа: value (Body/secondary) + chevron або toggle
```

---

### 6.9 Bottom Sheet

Для вибору з довгого списку (мова, AI-модель).

```
Background:    --color-bg-elevated
Border-radius: 20px 20px 0 0 (тільки верхні)
Padding:       24px 16px
Handle:        4px x 32px, rgba(255,255,255,0.20), radius-full, centered, margin-bottom: 20px

Overlay:       rgba(0,0,0,0.60)
Animation:     slide-up 300ms spring
```

---

### 6.10 AI Behavior Slider

Кастомний слайдер для налаштування "температури" AI.

```
3 фіксовані позиції: Чернетка | Стандарт | Дослідження
Track:     --color-bg-elevated, height 4px, radius-full
Thumb:     20px, --color-accent-ai (фіолетовий), shadow
Labels:    Caption/secondary під кожною позицією
Active track: --color-accent-ai
```

---

### 6.11 Header (TMA)

```
Height:        56px
Background:    --color-bg-primary (sticky, з blur якщо підтримується)
Padding:       0 16px

Left:          Back button (якщо є) або назва студії (Headline/Medium)
Center:        Назва екрану (Headline/Semibold)
Right:         Role Switcher badge або action button
```

**Role Switcher Badge:**
```
Дизайнер: bg rgba(212,175,55,0.15), text #D4AF37, "Режим дизайнера"
Клієнт:   bg rgba(45,135,255,0.15), text #2D87FF, "Режим клієнта"
```

---

## 7. Екрани

### 7.1 Settings Screen (перший екран для розробки)

**Призначення:** Конфігурація студії. Цей екран будує 70% компонентної бази.

**Інформаційна архітектура:**

```
Налаштування
│
├── Профіль і студія
│   ├── Аватар + ім'я студії
│   ├── Роль (Режим дизайнера / Режим клієнта)
│   └── Мова інтерфейсу (за замовчуванням: Українська)
│
├── AI-асистент             [секція з AI-стилем]
│   ├── Модель AI (Segmented: Claude / GPT-4o)
│   ├── Стиль відповіді (Segmented: Чернетка / Стандарт / Дослідження)
│   ├── Рівень креативності (Slider — 3 позиції)
│   └── Системний промпт (TextArea, приховано за "Розширені")
│
├── Інтеграції              [картки зі статусами]
│   ├── Notion Card (Підключено / Помилка / Не налаштовано)
│   ├── Google Drive Card
│   └── Google Calendar Card
│
├── Портфоліо і тарифи
│   ├── URL портфоліо
│   └── Валюта за замовчуванням (Bottom Sheet: UAH / USD / EUR)
│
├── Сповіщення
│   ├── Робочі години (time picker: від/до)
│   ├── Новий лід (Toggle)
│   ├── Нагадування про дедлайн (Toggle + days before)
│   └── Клієнт не відповідає (Toggle + days)
│
└── Система
    ├── Версія застосунку (Caption, non-interactive)
    ├── Експортувати мої дані (Ghost button)
    └── Видалити акаунт (Destructive button)
```

**Layout (TMA mobile):**
```
[Sticky Header: "Налаштування" + Role Badge]
[16px top padding]
[Section: Profile]        → List Rows
[32px gap]
[Section: AI Assistant]   → Card з Toggle + Segmented + Slider
[32px gap]
[Section: Integrations]   → 3x Integration Cards
[32px gap]
[Section: Portfolio]      → List Rows + Input
[32px gap]
[Section: Notifications]  → Card з Toggles
[32px gap]
[Section: System]         → List Rows
[100px bottom padding]    ← обов'язково для TMA Main Button
```

**Мовна політика Wave 1 (обов'язково):**
- Дефолт у `Settings > Мова інтерфейсу` = `Українська`
- Всі user-facing тексти у Wave 1 (TMA + Web) — українською
- Англійська локалізація допускається тільки як future scope, без зміни дефолту на старті

---

### 7.2 Designer Dashboard (наступний екран)

**Призначення:** Центр керування. Показує активні проєкти, нові ліди, найближчі дедлайни.

**Структура:**
```
[Header: "Студія" + New Lead count badge]

[Quick Stats Row]  ← 3 метрики в ряд: Активних / Нові ліди / Дедлайн
  Кожна: маленька картка, велика цифра (Display), підпис (Caption)

[Section: Нові звернення]
  Горизонтальний scroll → Preview Cards лідів

[Section: Активні проєкти]
  Вертикальний список → Project Cards зі статусом

[Section: Найближчі дедлайни]
  List Rows з датою та назвою проєкту

[TMA Main Button: "Нова дія"]
```

---

### 7.3 Brief Flow Screen (клієнтський екран)

**Призначення:** Покроковий AI-діалог для збору брифу.

**Структура:**
```
[Header: "Бриф" + Progress indicator (dots)]

[Question Card]
  Велика картка: питання (Headline) + підказка (Body/secondary)
  
[Options / Input Area]
  Якщо вибір: Chips (горизонтальний scroll або grid)
  Якщо текст: Large TextArea з floating label
  Якщо файл: Upload zone

[Navigation]
  Back (Ghost) + Continue (Primary або TMA Main Button)
  
[Progress bar] ← тонкий, --color-accent-gold, під хедером
```

---

## 8. Платформна адаптація

### TMA (Mobile — Telegram Mini App)

| Елемент | Рішення |
|---|---|
| Навігація | Bottom Tab Bar (V2) або Header back button |
| Вибір зі списку | Bottom Sheet (виїжджає знизу) |
| Основна дія | TMA Native Main Button (синій, знизу) |
| Форми | Full-screen, floating labels |
| Модалі | Bottom Sheet або Full-screen |
| Safe Area | Обов'язково враховувати top + bottom |

### Web (Desktop — майбутнє)

| Елемент | Рішення |
|---|---|
| Навігація | Sidebar зліва |
| Settings | 2-column: назва секції + контролери |
| Картки | Masonry grid або 2-3 колонки |
| Вибір | Context Menu / Dropdown |
| Основна дія | Fixed header button |
| Bottom Sheet → | Modal dialog у центрі екрану |

### Shared Tokens

Кольори, типографіка, spacing, radius — **ідентичні** для TMA та Web. Відрізняється тільки layout і патерни навігації.

---

## 9. Стани компонентів

Кожен інтерактивний компонент ПОВИНЕН мати всі стани задокументованими:

| Стан | Опис |
|---|---|
| Default | Нормальний вигляд |
| Hover | +10% brightness або background зміна (desktop) |
| Pressed | scale(0.97) + 5% darker |
| Loading | Spinner, контент fade 50% |
| Success | --color-success, checkmark animation |
| Error | --color-error, shake animation |
| Disabled | 30% opacity, cursor: not-allowed |
| Empty | Ілюстрація + текст заглушки |
| Skeleton | Animated shimmer (--color-bg-elevated → --color-bg-surface) |

### Анімації станів

```css
/* Success feedback */
@keyframes success-pulse {
  0% { transform: scale(1); }
  50% { transform: scale(1.05); }
  100% { transform: scale(1); }
}

/* Shake on error */
@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  75% { transform: translateX(4px); }
}

/* Skeleton shimmer */
@keyframes shimmer {
  0% { background-position: -200% center; }
  100% { background-position: 200% center; }
}
```
