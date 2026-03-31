# BaguetteDesign — Component Specs (Wave 1 Atoms v1)

> Статус: Active
> Scope: `QUEUE-016` — Input / Toggle / Segmented / Status Badge
> Мова UX: українська (default)

---

## 1. Загальні правила

1. Тільки token-first з `08-design-system.md` (без hardcoded ad-hoc кольорів у фінальному компоненті).
2. Мінімальна інтерактивна зона: `>= 44px` (клікабельна зона).
3. Мінімальний інтерактивний/операційний текст: `13px` (`12px` дозволений лише для secondary non-interactive metadata).
4. Всі user-facing demo тексти і placeholder — українською.
5. Підтримка станів: `default`, `loading`, `disabled`, `error` (де релевантно).

---

## 2. Input Field (Atom)

### 2.1 Figma Component Set

`Atom / Input Field`

Properties:
1. `state`: `default | focus | error | disabled | filled`
2. `hasLabel`: `true | false`
3. `hasHint`: `true | false`
4. `hasIconLeft`: `true | false`
5. `size`: `md` (Wave 1)

### 2.2 Visual Contract

1. Height: `52`
2. Padding X: `16`
3. Radius: `8`
4. Background: `--color-bg-input`
5. Border:
- default: `--color-border-default`
- focus: `--color-border-focus` + focus ring `0 0 0 3px rgba(212,175,55,0.15)`
- error: `--color-border-error`

### 2.3 Content Contract (UA)

Defaults:
1. Label: `Назва поля`
2. Placeholder: `Введіть значення`
3. Hint: `Пояснення до поля`
4. Error: `Перевірте правильність значення`

### 2.4 React Props (reference)

```ts
type InputState = "default" | "focus" | "error" | "disabled" | "filled";

interface InputFieldProps {
  label?: string;            // "Назва поля"
  placeholder?: string;      // "Введіть значення"
  value?: string;
  hint?: string;
  errorText?: string;
  disabled?: boolean;
  loading?: boolean;
  hasIconLeft?: boolean;
  state?: InputState;
}
```

---

## 3. Toggle (Atom)

### 3.1 Figma Component Set

`Atom / Toggle`

Properties:
1. `state`: `off | on | loading | disabled`
2. `labelMode`: `withLabel | noLabel`

### 3.2 Visual Contract

1. Track: `52x32`
2. Hit area (wrapper): `52x44` — **обов'язково ≥ 44×44px** (WCAG 2.5.8 / Apple HIG minimum touch target)
3. Thumb: `26x26`
4. Radius track: `full`
5. Colors:
- on: `--color-accent-gold`
- off: `--color-border-default`
- disabled: `opacity 30%`

### 3.3 Content Contract (UA)

Label examples:
1. `Новий лід`
2. `Нагадування про дедлайн`
3. `Клієнт не відповідає`

### 3.4 React Props (reference)

```ts
type ToggleState = "off" | "on" | "loading" | "disabled";

interface ToggleProps {
  checked: boolean;
  label?: string;            // "Новий лід"
  disabled?: boolean;
  loading?: boolean;
  state?: ToggleState;
}
```

---

## 4. Segmented Control (Atom)

### 4.1 Figma Component Set

`Atom / Segmented Control`

Properties:
1. `segments`: `2 | 3`
2. `selected`: `1 | 2 | 3`
3. `state`: `default | disabled`

### 4.2 Visual Contract

1. Height: `44`
2. Container padding: `4`
3. Radius: `8`
4. Container bg: `--color-neutral-soft`
5. Active segment:
- bg: `--color-bg-elevated`
- font: `15 / semibold`
- shadow: `--shadow-card`

### 4.3 Content Contract (UA)

Preset sets:
1. AI tone (3): `Чернетка | Стандарт | Дослідження`
2. Notifications mode (2): `Лише критичні | Повна підтримка`

### 4.4 React Props (reference)

```ts
interface SegmentOption {
  id: string;
  label: string;            // UA label
}

interface SegmentedControlProps {
  options: SegmentOption[];
  selectedId: string;
  disabled?: boolean;
}
```

---

## 5. Status Badge (Atom)

### 5.1 Figma Component Set

`Atom / Status Badge`

Properties:
1. `status`: `connected | error | pending | info | not-configured`
2. `dot`: `on | off`

### 5.2 Visual Contract

1. Padding: `4 10`
2. Radius: `full`
3. Font: `13 / medium`

Status mapping:
1. connected → bg `--color-success-soft`, text `--color-success`, label `Підключено`
2. error → bg `--color-error-soft`, text `--color-error`, label `Помилка`
3. pending → bg `--color-warning-soft`, text `--color-warning`, label `Очікує`
4. info → bg `--color-info-soft`, text `--color-info`, label `Інфо`
5. not-configured → bg `--color-neutral-soft`, text `--color-text-tertiary`, label `Не налаштовано`

### 5.3 React Props (reference)

```ts
type BadgeStatus = "connected" | "error" | "pending" | "info" | "not-configured";

interface StatusBadgeProps {
  status: BadgeStatus;
  label?: string;          // UA, optional override
  withDot?: boolean;
}
```

---

## 6. Станова матриця по атомах

| Component | default | loading | error | disabled |
|---|---|---|---|---|
| Input | yes | optional | yes | yes |
| Toggle | yes | yes | n/a | yes |
| Segmented | yes | n/a | n/a | yes |
| Badge | yes | n/a | via status=error | n/a |

---

## 7. Правила truncation для українських текстів

1. Довгі лейбли (> ширини контейнера): `text-overflow: ellipsis; overflow: hidden; white-space: nowrap`.
2. Не розривати слова всередині: `overflow-wrap: break-word` тільки для Body/Caption у блоках з обмеженою висотою.
3. Максимальна довжина лейблів у Badge/Toggle label — **24 символи** (враховуючи середню довжину українських слів ~6 символів).
4. Для Section Header (All-caps): перевірити, що All-caps кирилиця не збільшує ширину > 120% від латиниці (через ширші літери Ш, Щ, Ж).
5. Tooltip або expand-on-tap для обрізаного тексту — обов'язково для інтерактивних елементів.

---

## 8. iOS Dynamic Type

1. Усі компоненти з фіксованою висотою (`52px`, `44px`, `68px`) використовують `min-height` замість `height` у реалізації.
2. Шрифти масштабуються від 1× до 1.35× без втрати контенту.
3. У Figma: додати variant `accessibility` = `default | large-text` для Input Field і Toggle (демонстрація при 1.35× scale).

---

## 9. Acceptance Criteria для `QUEUE-016`

1. У Figma існують 4 component sets:
- `Atom / Input Field`
- `Atom / Toggle`
- `Atom / Segmented Control`
- `Atom / Status Badge`
2. У кожного set є variant properties зі специфікації цього файлу.
3. Demo-сцени атомів українською мовою.
4. Мінімальна touch зона не менше `44px` для інтерактивних контролів (WCAG 2.5.8).
5. Компоненти готові до складання в `Settings` і `Dashboard` без додаткових ad-hoc правок.
6. Truncation rules (секція 7) застосовані до всіх лейблів.
7. Dynamic Type variant (секція 8) існує для Input Field і Toggle.
