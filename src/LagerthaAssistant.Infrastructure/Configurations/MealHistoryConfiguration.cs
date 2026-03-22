namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MealHistoryConfiguration : IEntityTypeConfiguration<MealHistory>
{
    public void Configure(EntityTypeBuilder<MealHistory> builder)
    {
        builder.ToTable("MealHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Servings)
            .HasPrecision(4, 1)
            .IsRequired();

        builder.Property(x => x.ProteinGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.CarbsGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.FatGrams)
            .HasPrecision(6, 1);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.EatenAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.MealId);
        builder.HasIndex(x => x.EatenAt);
    }
}
