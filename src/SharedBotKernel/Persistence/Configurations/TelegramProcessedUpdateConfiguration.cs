namespace SharedBotKernel.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedBotKernel.Domain.Entities;

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
