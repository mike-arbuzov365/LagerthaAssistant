namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class VocabularyCardTokenConfiguration : IEntityTypeConfiguration<VocabularyCardToken>
{
    public void Configure(EntityTypeBuilder<VocabularyCardToken> builder)
    {
        builder.ToTable("VocabularyCardTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenNormalized)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(x => x.TokenNormalized);

        builder.HasIndex(x => new { x.VocabularyCardId, x.TokenNormalized })
            .IsUnique();
    }
}
