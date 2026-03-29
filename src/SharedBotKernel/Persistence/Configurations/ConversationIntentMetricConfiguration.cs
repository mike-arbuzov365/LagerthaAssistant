namespace SharedBotKernel.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedBotKernel.Domain.Entities;

public sealed class ConversationIntentMetricConfiguration : IEntityTypeConfiguration<ConversationIntentMetric>
{
    public void Configure(EntityTypeBuilder<ConversationIntentMetric> builder)
    {
        builder.ToTable("ConversationIntentMetrics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MetricDateUtc)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.AgentName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Intent)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.IsBatch)
            .IsRequired();

        builder.Property(x => x.Count)
            .IsRequired();

        builder.Property(x => x.TotalItems)
            .IsRequired();

        builder.Property(x => x.LastSeenAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.MetricDateUtc, x.Channel, x.AgentName, x.Intent, x.IsBatch })
            .IsUnique();

        builder.HasIndex(x => new { x.MetricDateUtc, x.Channel, x.Count });
    }
}
