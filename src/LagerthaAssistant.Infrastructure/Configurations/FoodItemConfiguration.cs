namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("FoodItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotionPageId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.IconEmoji)
            .HasMaxLength(32);

        builder.Property(x => x.Category)
            .HasMaxLength(64);

        builder.Property(x => x.Store)
            .HasMaxLength(128);

        builder.Property(x => x.Price)
            .HasPrecision(10, 2);

        builder.Property(x => x.Quantity)
            .HasMaxLength(64);

        builder.Property(x => x.NotionSyncStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.NotionLastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.NotionPageId).IsUnique();
        builder.HasIndex(x => new { x.NotionSyncStatus, x.NotionUpdatedAt });
        builder.HasIndex(x => x.Name);
    }
}
