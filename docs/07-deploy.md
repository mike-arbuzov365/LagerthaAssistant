# CI/CD і Деплой

> Автоматизовано, повторювано, без ручних кроків

---

## GitHub Actions Pipeline

```yaml
name: CI/CD
on:
  push:
    branches: [master]
  pull_request:

jobs:
  test:
    steps:
      - dotnet build
      - dotnet test

  deploy:
    needs: test
    if: github.ref == 'refs/heads/master'
    steps:
      - Push to Railway
      - Run migrations
      - Health check
      - Set Telegram webhook
```

---

## Railway Монорепо (два боти)

- Service **"lagertha"**: Root Dir = `/`, Dockerfile вказано явно
- Service **"baguette"**: Root Dir = `/`, окремий Dockerfile
- Один PostgreSQL сервіс — спільний для обох
- Env vars окремо для кожного сервісу
- Deploy triggers: тільки якщо змінились файли бота (path filters)

---

## Health Check

```
GET /health
→ 200 OK {"status":"healthy", "db":"connected", "telegram":"webhook_set"}
```

---

## Checklist перед production деплоєм

- [ ] Всі тести зелені
- [ ] Міграції застосовані (`dotnet ef database update`)
- [ ] `/health` повертає 200
- [ ] Webhook зареєстровано
- [ ] Secrets не в коді
- [ ] Логи налаштовані
- [ ] Rollback план є
- [ ] Дизайнер протестував вручну

---

## Результат

- Бот живий на Railway, деплоїться автоматично при push в `master`
- Pull Request → тести → merge → деплой → webhook → готово
- Весь процес: ~3 хвилини без ручного втручання
