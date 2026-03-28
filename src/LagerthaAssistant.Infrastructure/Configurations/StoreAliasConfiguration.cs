namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class StoreAliasConfiguration : IEntityTypeConfiguration<StoreAlias>
{
    public void Configure(EntityTypeBuilder<StoreAlias> builder)
    {
        builder.ToTable("StoreAliases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DetectedPattern)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.ResolvedStoreName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.DetectedPattern)
            .IsUnique();
    }
}
