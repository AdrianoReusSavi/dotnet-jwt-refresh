using JwtRefresh.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace JwtRefresh.Infrastructure.Context;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }
}