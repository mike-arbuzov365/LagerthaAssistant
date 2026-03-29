namespace SharedBotKernel.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedBotKernel.Domain.Entities;

public sealed class UserAiCredentialConfiguration : IEntityTypeConfiguration<UserAiCredential>
{
    public void Configure(EntityTypeBuilder<UserAiCredential> builder)
    {
        builder.ToTable("UserAiCredentials");

        builder.HasKey(x => new { x.Channel, x.UserId, x.Provider });

        builder.Property(x => x.Channel)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.EncryptedApiKey)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}
