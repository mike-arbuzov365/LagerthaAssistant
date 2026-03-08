namespace LagerthaAssistant.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LagerthaAssistant.Domain.Entities;

public sealed class UserMemoryEntryConfiguration : IEntityTypeConfiguration<UserMemoryEntry>
{
    public void Configure(EntityTypeBuilder<UserMemoryEntry> builder)
    {
        builder.ToTable("UserMemoryEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.LastSeenAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Key)
            .IsUnique();

        builder.HasIndex(x => new { x.IsActive, x.UpdatedAt });
    }
}

