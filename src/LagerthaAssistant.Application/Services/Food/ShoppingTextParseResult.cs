namespace LagerthaAssistant.Application.Services.Food;

public sealed record ShoppingTextParseResult(
    string ProductName,
    string? Quantity,
    string? Store);
