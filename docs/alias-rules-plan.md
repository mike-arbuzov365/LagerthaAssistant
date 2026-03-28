# Plan: Alias Rules for Stores and Unknown Items

## Overview

Two alias rule systems + one formatting fix:

1. **Formatting fix** — зробити "Невідомі позиції" та "Відфільтровані не-товари" однаковими
2. **Store alias rules** — автоматично асоціювати FOP/назву з чека з магазином у базі
3. **Item alias rules** — автоматично асоціювати невідомий товар з чека з існуючим товаром на складі

---

## Частина 1: Formatting fix (1 рядок коду)

**Файл:** `TelegramController.cs` → `BuildInventoryPhotoPreviewText`

```csharp
// До:
sb.AppendLine($"  {index + 1})🔹{session.NonProducts[index]}");

// Після:
sb.AppendLine($"{index + 1}) 🔹{session.NonProducts[index]}");
```

Обидві секції матимуть формат `1) 🔹text`.

---

## Частина 2: Store alias rules

### 2.1 Flows

**Flow A — перший чек від незнайомого магазину:**
```
AI детектує → "ФОП Бурда Сергій Михайлович"
     ↓ перевірка StoreAliases (немає)
     ↓ перевірка GetDistinctStores() (немає exact match)
Показати UI → "+ Додати новий / 🔗 Обрати існуючий / ⏭ Пропустити"
     ↓ юзер обирає "Nash Kray"
Зберегти → StoreAliases: "ФОП Бурда Сергій Михайлович" → "Nash Kray"
Продовжити → flow як завжди
```

**Flow B — наступний чек від того ж магазину:**
```
AI детектує → "ФОП Бурда Сергій Михайлович"
     ↓ перевірка StoreAliases → знайдено: "Nash Kray"
Auto-resolve → "Nash Kray" (без запиту юзера)
Продовжити → flow як завжди
```

**Flow C — Add as new store (теж зберігає alias):**
```
     ↓ юзер натискає "+ Додати новий магазин"
     ↓ ResolvedStoreName = DetectedStoreNameEn (як зараз)
Зберегти → StoreAliases: "ФОП Бурда Сергій Михайлович" → "Burda FOP" (EN назва від AI)
```

### 2.2 Нові файли

| Файл | Що |
|------|----|
| `Domain/Entities/StoreAlias.cs` | Entity: `Id`, `DetectedPattern`, `ResolvedStoreName`, `CreatedAt` |
| `Infrastructure/Configurations/StoreAliasConfiguration.cs` | EF: таблиця `StoreAliases`, `DetectedPattern` varchar(512) unique |
| `Infrastructure/Migrations/XXXXXXXX_AddAliasRules.cs` | EF міграція (генерується) |

### 2.3 Зміни існуючих файлів

**`AppDbContext.cs`**
```csharp
public DbSet<StoreAlias> StoreAliases => Set<StoreAlias>();
```

**`IFoodItemRepository.cs`** — нові методи:
```csharp
Task<string?> GetStoreByAliasAsync(string detectedPattern, CancellationToken ct);
Task SaveStoreAliasAsync(string detectedPattern, string resolvedStoreName, CancellationToken ct);
```

**`FoodItemRepository.cs`** — реалізація (upsert за `DetectedPattern`)

**`IFoodTrackingService.cs`** — нові методи:
```csharp
Task<string?> ResolveStoreAliasAsync(string detectedPattern, CancellationToken ct);
Task SaveStoreAliasAsync(string detectedPattern, string resolvedStoreName, CancellationToken ct);
```

**`FoodTrackingService.cs`** — реалізація (делегує до репозиторію)

**`TelegramController.cs`** — 2 місця:

1. У `AfterPhotoApplyAsync` / store detection block — перед `GetDistinctStoresAsync`:
```csharp
// Нова перевірка (до існуючого exact match):
var alias = await foodTrackingService.ResolveStoreAliasAsync(session.DetectedStoreNameEn, ct);
if (alias is not null)
{
    // auto-resolve, не показувати UI
    ...
    return await TransitionToUnknownItemsOrFinishAsync(...);
}
```

2. Після `PhotoStoreAdd` і `PhotoStoreSelectPrefix` handlers — зберігати alias:
```csharp
await _foodTrackingService.SaveStoreAliasAsync(
    session.DetectedStoreNameEn,
    resolvedStoreName,
    cancellationToken);
```

---

## Частина 3: Item alias rules

### 3.1 Flows

**Flow A — перший раз зустрічаємо невідомий товар:**
```
AI повертає unknown → "Vodka Hetman ICE 30%" (NameEn)
     ↓ перевірка ItemAliases (немає)
     ↓ залишається у секції "Невідомі позиції"
Показати UI → "Додати всі / Обрати номери / 🔗 Прив'язати до існуючих / Пропустити"
     ↓ юзер натискає "🔗 Прив'язати"
Бот питає → "Надішли пари: <номер>=<ID товару>, наприклад: 1=42, 3=15"
             (підказка: використай 'Всі товари' щоб знайти ID)
     ↓ юзер надсилає "1=42"
Зберегти → ItemAliases: "Vodka Hetman ICE 30%" → FoodItemId=42
Застосувати → +1 pcs до item ID 42 (як звичайний кандидат)
```

**Flow B — наступний чек з тим самим товаром:**
```
AI повертає unknown → "Vodka Hetman ICE 30%"
     ↓ перевірка ItemAliases → знайдено: FoodItemId=42
     ↓ переміщаємо з unknown до candidates з itemId=42
Застосовується автоматично як інші знайдені товари
```

### 3.2 Де alias шукається/зберігається

- Ключ: `DetectedNameEn` (рядок від AI, наприклад `"Vodka Hetman ICE 30%"`)
- Значення: `FoodItemId` (int, FK до `FoodItems`)
- Якщо `FoodItem` видалено → alias ігнорується (nullable or soft-check)

### 3.3 Нові файли

| Файл | Що |
|------|----|
| `Domain/Entities/ItemAlias.cs` | Entity: `Id`, `DetectedPattern`, `FoodItemId`, `CreatedAt` |
| `Infrastructure/Configurations/ItemAliasConfiguration.cs` | EF: `ItemAliases`, `DetectedPattern` varchar(512) unique, FK до `FoodItems` |

(міграція — та сама що для StoreAlias, один `AddAliasRules`)

### 3.4 Зміни існуючих файлів

**`AppDbContext.cs`**
```csharp
public DbSet<ItemAlias> ItemAliases => Set<ItemAlias>();
```

**`IFoodItemRepository.cs`** — нові методи:
```csharp
Task<int?> GetItemByAliasAsync(string detectedPattern, CancellationToken ct);
Task SaveItemAliasAsync(string detectedPattern, int foodItemId, CancellationToken ct);
```

**`FoodItemRepository.cs`** — реалізація

**`IFoodTrackingService.cs`** — нові методи:
```csharp
Task<int?> ResolveItemAliasAsync(string detectedPattern, CancellationToken ct);
Task SaveItemAliasAsync(string detectedPattern, int foodItemId, CancellationToken ct);
```

**`FoodTrackingService.cs`** — реалізація

**`CallbackDataConstants.cs`**
```csharp
public const string PhotoUnknownLink = "inventory:photo:unknown:link";
```

**`PendingTelegramModels.cs`**
```csharp
InventoryPhotoAwaitingItemLink = 12
```

**`TelegramNavigationPresenter.cs`** — `BuildPhotoUnknownItemsKeyboard`:
```
[✅ Додати всі]
[✏️ Обрати номери]  [🔗 Прив'язати]
[⏭ Пропустити]
```

**`ITelegramNavigationPresenter.cs`** — без змін (`BuildPhotoUnknownItemsKeyboard` вже є)

**`LocalizationService.cs`** — нові ключі:
```
inventory.photo.unknown.link           = "🔗 Прив'язати"
inventory.photo.unknown.link_prompt    = "Надішли пари <номер>=<ID>, наприклад: 1=42, 3=15\nID товару можна знайти у розділі 'Всі товари'."
inventory.photo.unknown.link_done      = "Прив'язано: {0}. Застосовано зміни."
inventory.photo.unknown.link_invalid   = "Невірний формат. Використай: 1=42 або 1=42, 3=15"
inventory.photo.unknown.link_not_found = "Товар з ID {0} не знайдено на складі."
```

**`TelegramController.cs`** — 3 місця:

1. У `HandleInventoryCallbackAsync` — новий handler для `PhotoUnknownLink`:
```csharp
if (callbackData == PhotoUnknownLink)
{
    pendingAction = InventoryPhotoAwaitingItemLink;
    return prompt("inventory.photo.unknown.link_prompt");
}
```

2. В `HandleInventoryTextAsync` — новий case для `InventoryPhotoAwaitingItemLink`:
```csharp
// парсинг "1=42, 3=15"
// для кожної пари: SaveItemAliasAsync + AdjustInventoryQuantityAsync
// видалити з unknown в сесії
```

3. У `BuildInventoryPhotoPreviewText` або перед ним — resolve aliases перед показом:
```csharp
// Для кожного unknown item:
var aliasItemId = await ResolveItemAliasAsync(entry.NameEn, ct);
if (aliasItemId.HasValue)
{
    // перемістити з unknown до candidates
}
```

---

## Порядок реалізації

```
[1] Formatting fix                   ← 1 рядок, окремий commit
[2] Domain: StoreAlias + ItemAlias   ← 2 нових entity
[3] EF: конфігурації + міграція      ← 1 міграція для обох
[4] Repository: 4 нових методи      ← IFoodItemRepository + impl
[5] Service: 4 нових методи         ← IFoodTrackingService + impl
[6] Store alias — controller flow    ← auto-resolve + save after pick
[7] Item alias — pre-resolve flow    ← перед показом preview
[8] Item alias — link UI + handler   ← кнопка + парсинг тексту
[9] Localization (all 6 langs)       ← нові ключі для link flow
```

## Файли, яких торкнеться зміна

| Файл | Тип змін |
|------|----------|
| `TelegramController.cs` | formatting fix + store auto-resolve + store save alias + item pre-resolve + item link handler |
| `Domain/Entities/StoreAlias.cs` | NEW |
| `Domain/Entities/ItemAlias.cs` | NEW |
| `Infrastructure/Configurations/StoreAliasConfiguration.cs` | NEW |
| `Infrastructure/Configurations/ItemAliasConfiguration.cs` | NEW |
| `Infrastructure/Data/AppDbContext.cs` | +2 DbSet |
| `Infrastructure/Migrations/XXXX_AddAliasRules.cs` | NEW (generated) |
| `Infrastructure/Repositories/FoodItemRepository.cs` | +4 методи |
| `Application/Interfaces/Repositories/Food/IFoodItemRepository.cs` | +4 методи |
| `Application/Services/Food/FoodTrackingService.cs` | +4 методи |
| `Application/Interfaces/Food/IFoodTrackingService.cs` | +4 методи |
| `Application/Constants/CallbackDataConstants.cs` | +PhotoUnknownLink |
| `Api/Models/PendingTelegramModels.cs` | +InventoryPhotoAwaitingItemLink |
| `Api/Services/TelegramNavigationPresenter.cs` | update BuildPhotoUnknownItemsKeyboard |
| `Api/Interfaces/ITelegramNavigationPresenter.cs` | без змін |
| `Infrastructure/Services/LocalizationService.cs` | +5 нових ключів × 6 мов |
| `IntegrationTests/TelegramControllerTests.cs` | fake presenter (якщо потрібно) |

**Разом: ~17 файлів, 1 нова міграція, ~2 нових entity.**
