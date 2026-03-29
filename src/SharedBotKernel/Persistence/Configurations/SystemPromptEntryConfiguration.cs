namespace SharedBotKernel.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedBotKernel.Domain.Entities;

public sealed class SystemPromptEntryConfiguration : IEntityTypeConfiguration<SystemPromptEntry>
{
    public void Configure(EntityTypeBuilder<SystemPromptEntry> builder)
    {
        builder.ToTable("SystemPromptEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PromptText)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Version)
            .IsUnique();

        builder.HasIndex(x => x.IsActive)
            .HasFilter("\"IsActive\" = TRUE")
            .IsUnique();
    }
}
