namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ItemAliasConfiguration : IEntityTypeConfiguration<ItemAlias>
{
    public void Configure(EntityTypeBuilder<ItemAlias> builder)
    {
        builder.ToTable("ItemAliases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DetectedPattern)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.FoodItemId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.DetectedPattern)
            .IsUnique();

        builder.HasOne(x => x.FoodItem)
            .WithMany()
            .HasForeignKey(x => x.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
