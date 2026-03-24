namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class GroceryListItemConfiguration : IEntityTypeConfiguration<GroceryListItem>
{
    public void Configure(EntityTypeBuilder<GroceryListItem> builder)
    {
        builder.ToTable("GroceryListItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotionPageId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasMaxLength(64);

        builder.Property(x => x.EstimatedCost)
            .HasPrecision(10, 2);

        builder.Property(x => x.Store)
            .HasMaxLength(128);

        builder.Property(x => x.NotionSyncStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.NotionLastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.FoodItem)
            .WithMany(x => x.GroceryListItems)
            .HasForeignKey(x => x.FoodItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => x.ArchivedAt == null);

        builder.HasIndex(x => x.NotionPageId).IsUnique();
        builder.HasIndex(x => x.IsBought);
        builder.HasIndex(x => new { x.NotionSyncStatus, x.NotionUpdatedAt });
    }
}
