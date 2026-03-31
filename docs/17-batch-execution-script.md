# BaguetteDesign — One-shot Batch Execution Script

> Статус: Active
> Формат: покроковий сценарій одного MCP-підключення
> Scope: `QUEUE-001...QUEUE-017`
> Hard gate: запуск тільки після явного апруву власника

---

## 1. Мета

За одне підключення до Figma MCP застосувати весь офлайн-пакет Wave 1:

1. Foundations (tokens, styles, базові борди)
2. Wireframes (Settings + Dashboard, TMA + Web)
3. Visual pass + QA pass
4. Localization baseline (UA default + UA copy)
5. Atoms v1 + interaction/state support

---

## 2. Вхідні параметри

Перед запуском зафіксувати:

1. `TARGET_FILE`: URL або file key нового Figma-файлу
2. `QUEUE_RANGE`: `QUEUE-001...QUEUE-017`
3. `COPY_SOURCE`: `docs/14-ux-copy-wave1-ua.md`
4. `INTERACTION_SOURCE`: `docs/16-interaction-matrix-wave1.md`
5. `PREFLIGHT_CMD`: `powershell -ExecutionPolicy Bypass -File .\scripts\figma-one-shot-preflight.ps1`

---

## 3. Стартовий gate

Запуск дозволений тільки якщо є фраза:

`АПРУВ MCP FIGMA: дозволяю одне підключення для батч-застосування QUEUE-001...QUEUE-017 у файлі [file-key/url].`

Без цієї фрази — виконання заборонене.

### 3.1 Preflight before launch

1. Виконати `PREFLIGHT_CMD`.
2. Заповнити `docs/18-one-shot-dry-run-report.md`.
3. Переконатися, що preflight result = `READY`.
4. До отримання апруву власника не підключатися до Figma MCP.

---

## 4. Freeze rule (анти-scope-creep)

Під час батчу:

1. Не додавати нові задачі поза `QUEUE-001...017`.
2. Не змінювати архітектуру IA.
3. Не робити стилістичні експерименти, яких нема в `08-design-system.md`.
4. Якщо виник блокер — stop + log, без "швидких обхідних" рішень.

---

## 5. Послідовність виконання

## Phase A — Foundations (`QUEUE-001...005`)

1. `QUEUE-004`: створити сторінки:
- `00 Foundations`
- `01 Settings WF`
- `02 Dashboard WF`

2. `QUEUE-001`: створити Figma Variables:
- `Colors/*`
- `Spacing/*`
- `Radius/*`

3. `QUEUE-002`: створити Text Styles:
- Display/Title/Headline/Body/Label/Caption

4. `QUEUE-003`: створити `Button` component set (variant/size/state)

5. `QUEUE-005`: створити борд `Wave 1 Foundations`:
- `Foundation / Tokens`
- `Foundation / States`

Checkpoint A:
- сторінки існують
- variables/styles існують
- button set існує
- foundations board існує

## Phase B — Wireframes (`QUEUE-006...007`)

1. `QUEUE-006`: `Settings / TMA` + `Settings / Web Adaptation`
2. `QUEUE-007`: `Dashboard / TMA` + `Dashboard / Web Adaptation`

Checkpoint B:
- 4 wireframe-екрани створені
- safe areas є в TMA екранах
- IA TMA == IA Web (без втрати секцій)
- Web adaptation перевірена на breakpoint `768px` (tablet) — всі секції видимі, layout не ламається

## Phase C — Visual + Components (`QUEUE-008,010,011`)

1. `QUEUE-008`: visual pass v1
- темна база + gold accent
- типографіка по token scale
- state colors

2. `QUEUE-010`: core molecules set + Section Header atom
- Integration Card (molecule)
- KPI Card (molecule)
- Action Row (molecule)
- Alert Row (molecule)
- Section Header (atom — див. `docs/08` §6.7)

3. `QUEUE-011`: state demo board
- Loading/Error/Offline/Empty/Disabled

Checkpoint C:
- візуал стабільний
- state demo існує
- core molecules готові

## Phase D — Localization + Atoms + Interaction (`QUEUE-013...017`)

1. `QUEUE-013`: language baseline
- default language в Settings = `Українська`
- жодного англомовного user-facing copy у Wave 1

2. `QUEUE-014`: застосувати Settings copy deck з `docs/14-ux-copy-wave1-ua.md`
3. `QUEUE-015`: застосувати Dashboard/state copy deck з `docs/14-ux-copy-wave1-ua.md`

4. `QUEUE-016`: atoms set v1 з `docs/15-component-specs-wave1.md`
- Input Field
- Toggle
- Segmented Control
- Status Badge

5. `QUEUE-017`: interaction/state support з `docs/16-interaction-matrix-wave1.md`
- transitions
- mock data packs (Normal/Heavy/Incident)

Checkpoint D:
- default UA зафіксований
- copy всюди український
- atoms v1 готові
- interaction scenarios покриті

## Phase E — Final QA + Hygiene (`QUEUE-009,012`)

1. `QUEUE-009`: QA + polish
- touch targets >= 44
- інтерактивний/операційний текст >= 13 (`12px` тільки для non-interactive caption)
- loading/offline/error покриті

2. `QUEUE-012`: naming/layer hygiene

Checkpoint E:
- фінальний QA завершений після локалізації
- неймінг чистий і передбачуваний

---

## 6. Стоп-умови

Негайно зупинити батч, якщо:

1. Немає доступу до `TARGET_FILE`
2. Permission/rate limit блокує продовження
3. Конфлікт структури, що ламає порядок фаз
4. Критичне розходження з token/copy source of truth

Дія після зупинки:

1. Не робити часткові імпровізовані правки
2. Записати блокер у `figma-queue.md` біля відповідного QUEUE
3. Запросити новий апрув на наступний батч

---

## 7. Вихід з батчу (обов'язково)

Після завершення:

1. Позначити `QUEUE-001...QUEUE-017` як `[x] Applied`
2. Оновити `Останнє застосування` у `figma-queue.md`
3. Додати рядок у `Журнал апрувів`
4. Зафіксувати короткий batch log:
- що застосовано
- що відкладено (якщо є)
- які ризики/борг лишився

---

## 8. Batch Log Template

```markdown
## Batch Log — [YYYY-MM-DD]
File: [file-key/url]
Range: QUEUE-001...QUEUE-017
Result: [Completed | Partial | Aborted]

Applied:
- QUEUE-...

Deferred:
- QUEUE-... (why)

Blockers:
- ...

Notes:
- ...
```
