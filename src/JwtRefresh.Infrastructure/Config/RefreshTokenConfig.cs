using JwtRefresh.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JwtRefresh.Infrastructure.Config;

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.UserId);

        builder.Property(p => p.FamilyId);

        builder.HasIndex(i => i.TokenHash)
            .IsUnique();

        builder.Property(p => p.CreatedAt);

        builder.Property(p => p.ExpiresAt);

        builder.Property(p => p.SessionExpiresAt);

        builder.Property(p => p.UsedAt);

        builder.Property(p => p.RevokedAt);
    }
}