namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MealIngredientConfiguration : IEntityTypeConfiguration<MealIngredient>
{
    public void Configure(EntityTypeBuilder<MealIngredient> builder)
    {
        builder.ToTable("MealIngredients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.FoodItem)
            .WithMany(x => x.MealIngredients)
            .HasForeignKey(x => x.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.MealId, x.FoodItemId }).IsUnique();
        builder.HasIndex(x => x.FoodItemId);
    }
}
