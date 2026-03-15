namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class TelegramProcessedUpdateConfiguration : IEntityTypeConfiguration<TelegramProcessedUpdate>
{
    public void Configure(EntityTypeBuilder<TelegramProcessedUpdate> builder)
    {
        builder.ToTable("TelegramProcessedUpdates");

        builder.HasKey(x => x.UpdateId);

        builder.Property(x => x.UpdateId)
            .ValueGeneratedNever();

        builder.Property(x => x.ProcessedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.ProcessedAtUtc);
    }
}
