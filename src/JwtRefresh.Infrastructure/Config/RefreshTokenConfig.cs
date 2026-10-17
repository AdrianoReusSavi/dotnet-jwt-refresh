using JwtRefresh.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JwtRefresh.Infrastructure.Config;

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(p => p.UserId)
            .HasColumnName("user_id");

        builder.HasIndex(i => i.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        builder.Property(p => p.FamilyId)
            .HasColumnName("family_id");

        builder.HasIndex(i => i.TokenHash)
            .HasDatabaseName("ux_refresh_tokens_token_hash")
            .IsUnique();

        builder.Property(p => p.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(p => p.SessionExpiresAt)
            .HasColumnName("session_expires_at");

        builder.Property(p => p.UsedAt)
            .HasColumnName("used_at");

        builder.Property(p => p.RevokedAt)
            .HasColumnName("revoked_at");
    }
}