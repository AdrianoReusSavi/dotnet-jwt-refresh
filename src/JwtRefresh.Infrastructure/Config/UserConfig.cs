using JwtRefresh.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JwtRefresh.Infrastructure.Config;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(p => p.Id)
            .HasName("pk_users");

        builder.Property(p => p.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.HasIndex(i => i.Email)
            .HasDatabaseName("ux_users_email")
            .IsUnique();

        builder.Property(p => p.Email)
            .HasColumnName("email")
            .HasMaxLength(254);

        builder.Property(p => p.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(60);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");
    }
}