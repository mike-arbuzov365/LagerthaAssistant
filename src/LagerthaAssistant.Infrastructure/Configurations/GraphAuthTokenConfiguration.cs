namespace LagerthaAssistant.Infrastructure.Configurations;

using LagerthaAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class GraphAuthTokenConfiguration : IEntityTypeConfiguration<GraphAuthToken>
{
    public void Configure(EntityTypeBuilder<GraphAuthToken> builder)
    {
        builder.ToTable("GraphAuthTokens");

        builder.HasKey(x => x.Provider);

        builder.Property(x => x.Provider)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.AccessToken)
            .IsRequired();

        builder.Property(x => x.RefreshToken)
            .IsRequired();

        builder.Property(x => x.AccessTokenExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}
