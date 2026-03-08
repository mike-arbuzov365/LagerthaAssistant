namespace LagerthaAssistant.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LagerthaAssistant.Domain.Entities;

public sealed class ConversationSessionConfiguration : IEntityTypeConfiguration<ConversationSession>
{
    public void Configure(EntityTypeBuilder<ConversationSession> builder)
    {
        builder.ToTable("ConversationSessions");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.SessionKey)
            .IsUnique();

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.ConversationSession)
            .HasForeignKey(x => x.ConversationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

