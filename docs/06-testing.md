# Тестування

> Для соло-розробника: швидко, практично, без over-engineering

---

## Піраміда тестів для бота

- **Unit тести (60%)** — доменна логіка: `BriefValidator`, `PriceCalculator`, `RoleRouter`
- **Integration тести (30%)** — сервіси з реальною БД (Testcontainers)
- **E2E тести (10%)** — ключові happy paths через Telegram API

## Що НЕ тестувати

- CRUD репозиторії (EF Core вже протестований)
- Конфігурацію і DI registration
- Зовнішні API (Notion, Google) — мокайте їх

---

## Приклад Unit тесту для бота

```csharp
// Тестуємо бізнес-логіку, не Telegram
[Fact]
public void RoleRouter_GivenAdminId_ReturnsDesigner()
{
    var router = new RoleRouter(adminId: 12345);
    var role = router.Resolve(userId: 12345);
    Assert.Equal(UserRole.Designer, role);
}

[Fact]
public void BriefValidator_MissingBudget_IsIncomplete()
{
    var brief = new Brief { ServiceType = "logo" };
    var result = new BriefValidator().Validate(brief);
    Assert.False(result.IsComplete);
    Assert.Contains("budget", result.MissingFields);
}
```

---

## Integration тест з Testcontainers (.NET)

```csharp
// Реальна PostgreSQL в Docker — без моків БД
public class BriefRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();

    [Fact]
    public async Task SaveBrief_ThenLoad_ReturnsCorrectData()
    {
        // Arrange: реальна БД, реальний EF context
        var repo = new BriefRepository(CreateContext());
        var brief = new Brief { ... };

        // Act
        await repo.SaveAsync(brief);
        var loaded = await repo.GetByIdAsync(brief.Id);

        // Assert
        Assert.Equal(brief.StructuredData, loaded.StructuredData);
    }
}
```

---

## Команди

```bash
# Всі тести
dotnet test BotPlatform.sln

# Тільки unit тести (швидко)
dotnet test tests/LagerthaAssistant.Application.Tests
dotnet test tests/LagerthaAssistant.Domain.Tests

# Integration тести (потрібен Docker)
dotnet test tests/LagerthaAssistant.IntegrationTests

# З покриттям
dotnet test --collect:"XPlat Code Coverage"
```

---

## Поточний стан тестів (Lagertha)

| Проект | Тестів | Статус |
|---|---|---|
| `LagerthaAssistant.Domain.Tests` | 5 | Зелені |
| `LagerthaAssistant.Application.Tests` | 491 | Зелені |
| `LagerthaAssistant.IntegrationTests` | 330 | Зелені |
| **Всього** | **826** | **Зелені** |
