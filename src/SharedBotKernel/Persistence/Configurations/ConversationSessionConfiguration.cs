namespace SharedBotKernel.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedBotKernel.Domain.Entities;

public sealed class ConversationSessionConfiguration : IEntityTypeConfiguration<ConversationSession>
{
    public void Configure(EntityTypeBuilder<ConversationSession> builder)
    {
        builder.ToTable("ConversationSessions");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.SessionKey)
            .IsUnique();

        builder.Property(x => x.Channel)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ConversationId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.CurrentSection)
            .HasMaxLength(32);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Channel, x.UserId, x.ConversationId, x.UpdatedAt });

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.ConversationSession)
            .HasForeignKey(x => x.ConversationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
