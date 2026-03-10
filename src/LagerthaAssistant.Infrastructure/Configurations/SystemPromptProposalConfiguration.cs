namespace LagerthaAssistant.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LagerthaAssistant.Domain.Entities;

public sealed class SystemPromptProposalConfiguration : IEntityTypeConfiguration<SystemPromptProposal>
{
    public void Configure(EntityTypeBuilder<SystemPromptProposal> builder)
    {
        builder.ToTable("SystemPromptProposals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProposedPrompt)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ReviewedAtUtc);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
