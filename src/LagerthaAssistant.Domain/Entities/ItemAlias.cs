namespace LagerthaAssistant.Domain.Entities;


public sealed class ItemAlias : AuditableEntity
{
    public string DetectedPattern { get; set; } = string.Empty;

    public int FoodItemId { get; set; }

    public FoodItem? FoodItem { get; set; }
}
