namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class VocabularySyncJobConfiguration : IEntityTypeConfiguration<VocabularySyncJob>
{
    public void Configure(EntityTypeBuilder<VocabularySyncJob> builder)
    {
        builder.ToTable("VocabularySyncJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestedWord)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.AssistantReply)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.TargetDeckFileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.TargetDeckPath)
            .HasMaxLength(1000);

        builder.Property(x => x.OverridePartOfSpeech)
            .HasMaxLength(32);

        builder.Property(x => x.StorageMode)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.LastAttemptAtUtc);

        builder.Property(x => x.CompletedAtUtc);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
