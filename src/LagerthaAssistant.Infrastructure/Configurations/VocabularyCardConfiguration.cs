namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class VocabularyCardConfiguration : IEntityTypeConfiguration<VocabularyCard>
{
    public void Configure(EntityTypeBuilder<VocabularyCard> builder)
    {
        builder.ToTable("VocabularyCards");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Word)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.NormalizedWord)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.Meaning)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.Examples)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.PartOfSpeechMarker)
            .HasMaxLength(32);

        builder.Property(x => x.DeckFileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.DeckPath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.LastKnownRowNumber)
            .IsRequired();

        builder.Property(x => x.StorageMode)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SyncStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.LastSyncError)
            .HasMaxLength(2000);

        builder.Property(x => x.FirstSeenAtUtc)
            .IsRequired();

        builder.Property(x => x.LastSeenAtUtc)
            .IsRequired();

        builder.Property(x => x.SyncedAtUtc);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasMany(x => x.Tokens)
            .WithOne(x => x.VocabularyCard)
            .HasForeignKey(x => x.VocabularyCardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.NormalizedWord, x.DeckFileName, x.StorageMode })
            .IsUnique();

        builder.HasIndex(x => new { x.StorageMode, x.LastSeenAtUtc });

        builder.HasIndex(x => new { x.SyncStatus, x.UpdatedAt });
    }
}
