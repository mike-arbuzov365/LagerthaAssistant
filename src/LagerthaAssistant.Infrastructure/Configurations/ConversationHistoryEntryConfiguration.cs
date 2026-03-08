namespace LagerthaAssistant.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LagerthaAssistant.Domain.Entities;

public sealed class ConversationHistoryEntryConfiguration : IEntityTypeConfiguration<ConversationHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ConversationHistoryEntry> builder)
    {
        builder.ToTable("ConversationHistoryEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.SentAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.ConversationSessionId, x.SentAtUtc });
    }
}

