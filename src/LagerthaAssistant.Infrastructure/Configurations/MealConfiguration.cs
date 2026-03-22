namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("Meals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotionPageId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ProteinGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.CarbsGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.FatGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.NotionSyncStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.NotionLastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.Ingredients)
            .WithOne(x => x.Meal)
            .HasForeignKey(x => x.MealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.History)
            .WithOne(x => x.Meal)
            .HasForeignKey(x => x.MealId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.NotionPageId).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => new { x.NotionSyncStatus, x.NotionUpdatedAt });
    }
}
