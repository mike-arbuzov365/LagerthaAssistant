# BaguetteDesign — One-shot Figma Transfer Runbook

> Статус: Active
> Мета: За одне MCP-підключення перенести офлайн-пакет Wave 1 у новий Figma акаунт
> Hard gate: тільки після явного апруву власника

---

## 1. Вхідні умови

Перед стартом мають бути виконані всі умови:

1. Є новий Figma файл у новому акаунті (URL або file key)
2. Є явний апрув у шаблоні з `docs/figma-queue.md`
3. `docs/figma-queue.md` містить pending записи `QUEUE-001...QUEUE-017`
4. Немає нових незадокументованих змін поза queue
5. `docs/14-ux-copy-wave1-ua.md` затверджено як source of truth для текстів Wave 1
6. `docs/16-interaction-matrix-wave1.md` затверджено як source of truth для interaction/state сценаріїв
7. `docs/17-batch-execution-script.md` затверджено як execution sequence для one-shot
8. `scripts/figma-one-shot-preflight.ps1` виконано з результатом `READY`
9. `docs/18-one-shot-dry-run-report.md` заповнено і має статус `READY`

---

## 2. Обсяг one-shot батчу

У межах одного підключення застосувати:

1. `QUEUE-001` Variables (tokens)
2. `QUEUE-002` Text styles
3. `QUEUE-003` Button atom
4. `QUEUE-004` File structure pages
5. `QUEUE-005` Foundations board
6. `QUEUE-006` Settings wireframes (TMA + Web)
7. `QUEUE-007` Dashboard wireframes (TMA + Web)
8. `QUEUE-008` Visual pass v1
9. `QUEUE-009` QA + polish
10. `QUEUE-010` Core Molecules set
11. `QUEUE-011` State Demo board
12. `QUEUE-012` Naming/Layer hygiene pass
13. `QUEUE-013` Localization baseline (default UA)
14. `QUEUE-014` Settings copy deck (UA)
15. `QUEUE-015` Dashboard/state copy deck (UA)
16. `QUEUE-016` Atoms set v1 (Input/Toggle/Segmented/Badge)
17. `QUEUE-017` Interaction matrix + mock data packs

---

## 3. Порядок виконання в підключенні

Застосовувати тільки в цій послідовності:

1. Foundations: variables + typography
2. Atom(s): Button
3. File skeleton: pages/sections
4. Screens: Settings WF, Dashboard WF
5. Visual pass
6. Molecules + state demo
7. Localization + UX copy (UA)
8. Atoms v1 pass (Input/Toggle/Segmented/Badge) + interaction data
9. Final QA + naming hygiene

Причина: спочатку стабільні токени та стилі, потім екрани, потім полірування.

---

## 4. Стоп-умови (abort conditions)

Зупинити батч і повернутись в офлайн, якщо:

1. Немає доступу до нового файлу
2. Є конфлікт у структурі сторінок або назвах, що ламає чергу
3. Інструментний ліміт або permission error не дає завершити пакет
4. Виявлено критичну неузгодженість токенів з `08-design-system.md`

У такому випадку:

1. Не робити часткових “допиляти пізніше” рішень в тій самій сесії
2. Описати блокер у `figma-queue.md` в коментарі до відповідного QUEUE
3. Дочекатись нового апруву на наступний батч

---

## 5. Вихідні артефакти після батчу

Після завершення:

1. Позначити `QUEUE-001...QUEUE-017` як `[x] Applied`
2. Оновити дату `Останнє застосування` у `figma-queue.md`
3. Додати рядок у `Журнал апрувів`
4. Коротко зафіксувати, що увійшло в батч (без нових scope creep)

---

## 6. Шаблон апруву для запуску

Власник має надіслати:

`АПРУВ MCP FIGMA: дозволяю одне підключення для батч-застосування QUEUE-001...QUEUE-017 у файлі [file-key/url].`

Без цього формату запуск one-shot батчу заборонений.
