using JwtRefresh.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace JwtRefresh.Infrastructure.Tests.Base;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = 
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public DataContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new DataContext(options);
    }

    public async Task DisposeAsync()
        => await _container.DisposeAsync();
}