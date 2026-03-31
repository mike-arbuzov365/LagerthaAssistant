# BaguetteDesign — Чеклист валідації дизайну (офлайн)

> Статус: Active
> Мета: фінальна перевірка офлайн-пакету перед Figma-деплоєм
> Використовується разом із `docs/18-one-shot-dry-run-report.md`

---

## 1. Токени та контраст

- [ ] `--color-text-tertiary` має контраст ≥ 4.5:1 на `--color-bg-primary` (WCAG AA)
- [ ] `--color-text-secondary` має контраст ≥ 4.5:1 на `--color-bg-primary`
- [ ] Усі semantic кольори (`success`, `error`, `warning`, `info`) мають контраст ≥ 3:1 на відповідних `-soft` фонах
- [ ] Жодних hardcoded ad-hoc кольорів — тільки token references

## 2. Touch targets

- [ ] Усі інтерактивні елементи мають hit area ≥ 44×44px
- [ ] Toggle wrapper: підтверджено `52×44`
- [ ] Button Small: підтверджено висота `44px`
- [ ] Segmented Control: підтверджено висота `44px`

## 3. Типографіка та Dynamic Type

- [ ] Мінімальний інтерактивний текст: `13px`
- [ ] `12px` використовується тільки для non-interactive caption/metadata
- [ ] Section Header: `13px / Bold / All-caps / letter-spacing: 0.5px`
- [ ] Фіксовані висоти замінені на `min-height` для підтримки Dynamic Type 1.35×
- [ ] Input Field та Toggle мають `accessibility` variant у Figma

## 4. Локалізація (UA)

- [ ] Усі user-facing тексти українською
- [ ] Жодного англомовного copy у Wave 1 екранах
- [ ] `docs/14-ux-copy-wave1-ua.md` є єдиним джерелом copy
- [ ] `docs/18-one-shot-dry-run-report.md` українською
- [ ] Truncation rules застосовані до всіх лейблів (max 24 символи для Badge/Toggle)
- [ ] All-caps кирилиця перевірена на ширину (Ш, Щ, Ж)

## 5. Станова матриця

- [ ] Input Field: default / focus / error / disabled / filled
- [ ] Toggle: off / on / loading / disabled
- [ ] Segmented Control: default / disabled
- [ ] Status Badge: connected / error / pending / info / not-configured
- [ ] Integration Card: connected / error / not-configured / loading / offline / retry-pending

## 6. Interaction Matrix

- [ ] Кожна ключова дія Settings має `success / error / offline` copy
- [ ] Кожна ключова дія Dashboard має `success / error / offline` copy
- [ ] 3 mock data packs існують: Normal / Heavy / Incident

## 7. Структура та naming

- [ ] Section Header класифіковано як Atom
- [ ] Integration Card / KPI Card / Action Row / Alert Row — molecules
- [ ] Naming у Figma layers відповідає `docs/08-design-system.md` §6
- [ ] Сторінки: `00 Foundations`, `01 Settings WF`, `02 Dashboard WF`

## 8. Платформна адаптація

- [ ] TMA safe areas: top `56px + env(safe-area-inset-top)`, bottom `100px`
- [ ] Web breakpoint `768px` перевірено — жодна секція не втрачається
- [ ] Layout Settings (TMA): sticky header + vertical scroll + 100px bottom padding
- [ ] Layout Dashboard (TMA): header + KPI row + sections + TMA Main Button

## 9. Документи — cross-reference

- [ ] `docs/08` → `docs/15`: token references узгоджені
- [ ] `docs/14` → `docs/15`: copy keys відповідають component content contracts
- [ ] `docs/16` → `docs/17`: interaction scenarios покривають усі QUEUE items
- [ ] `docs/17` → `docs/18`: batch script references коректні
