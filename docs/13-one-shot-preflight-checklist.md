# BaguetteDesign — One-shot Preflight Checklist

> Використовувати прямо перед MCP-запуском.

---

## A. Access & File

- [ ] Новий Figma акаунт активний
- [ ] Відкривається цільовий файл (URL/file key)
- [ ] Є права редагування у файлі
- [ ] Порожня або контрольована стартова структура файлу

---

## B. Approval Gate

- [ ] Власник надіслав апрув у точному шаблоні
- [ ] Діапазон у апруві: `QUEUE-001...QUEUE-017`
- [ ] Апрув зафіксований у `docs/figma-queue.md` → `Журнал апрувів`

---

## C. Queue Integrity

- [ ] Всі `QUEUE-001...QUEUE-017` мають статус `[ ] Pending`
- [ ] Немає нових задач поза queue
- [ ] Немає конфліктних формулювань між queue і runbook
- [ ] `scripts/figma-one-shot-preflight.ps1` завершився зі статусом `READY`
- [ ] `docs/14-ux-copy-wave1-ua.md` погоджено як source of truth для UX текстів
- [ ] `docs/16-interaction-matrix-wave1.md` погоджено як source of truth для interaction/state сценаріїв
- [ ] `docs/17-batch-execution-script.md` затверджено як покроковий сценарій запуску

---

## D. Execution Order Lock

- [ ] Послідовність береться з `docs/11-figma-one-shot-runbook.md`
- [ ] Не додавати новий scope під час батчу
- [ ] Якщо блокер — зупинка і фіксація, без імпровізації
- [ ] Локалізаційний baseline зафіксовано: default language = Українська

---

## E. Dry-run Evidence

- [ ] Заповнено `docs/18-one-shot-dry-run-report.md`
- [ ] У dry-run report зафіксовано: `no Figma connection before approval`
- [ ] `docs/19-one-shot-command-center.md` використано як launch-day точку входу

---

## F. Exit Criteria

- [ ] Усі задачі `QUEUE-001...QUEUE-017` застосовані
- [ ] Статуси оновлено на `[x] Applied`
- [ ] Оновлено `Останнє застосування` у queue
- [ ] Додано фінальний короткий батч-лог
