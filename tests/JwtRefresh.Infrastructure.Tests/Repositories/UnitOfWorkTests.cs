using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Services;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Domain.Enums;
using JwtRefresh.Infrastructure.Context;
using JwtRefresh.Infrastructure.Repositories;
using JwtRefresh.Infrastructure.Tests.Base;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JwtRefresh.Infrastructure.Tests.Repositories;

public class UnitOfWorkTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private readonly FakeTimeProvider _timeProvider = new(Now);

    private RefreshTokenService CreateService(DataContext context, IUnitOfWork? uow = null)
        => new(
            new RefreshTokenRepository(context),
            uow ?? new UnitOfWork(context),
            _timeProvider,
            Options.Create(new RefreshTokenOptions { Lifetime = Lifetime, SessionLifetime = SessionLifetime })
        );

    [Fact]
    public async Task DiscardChanges_Lets_The_Family_Revocation_Retry_See_The_Concurrent_Successor()
    {
        await using var issueContext = fixture.CreateContext();
        await using var refreshContext = fixture.CreateContext();
        await using var victimContext = fixture.CreateContext();
        await using var attackerContext = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var issueService = CreateService(issueContext);
        var refreshService = CreateService(refreshContext);
        
        var victimUow = new UnitOfWork(victimContext);
        var victimRepository = new RefreshTokenRepository(victimContext);

        var readRepository = new RefreshTokenRepository(readContext);

        var rawValue = await issueService.IssueAsync(await fixture.CreateUserAsync());

        _timeProvider.Advance(TimeSpan.FromDays(1));

        var result = await refreshService.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Refreshed, result.Status);

        _timeProvider.Advance(TimeSpan.FromDays(1));

        var attackerUow = new InterferingUnitOfWork(new UnitOfWork(attackerContext), async () =>
        {
            var token = await victimRepository.GetByHashAsync(RefreshToken.Hash(result.RawValue!));

            Assert.NotNull(token);

            var (status, successor, _) = token.Rotate(_timeProvider.GetUtcNow(), Lifetime);

            Assert.Equal(RotationStatus.Rotated, status);
            Assert.NotNull(successor);

            victimRepository.Add(successor);

            await victimUow.CommitAsync();
        });

        var attackerService = CreateService(attackerContext, attackerUow);
        var reuse = await attackerService.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.ReuseDetected, reuse.Status);
        Assert.Equal(1, attackerUow.InterferenceCount);

        var firstToken = await readRepository.GetByHashAsync(RefreshToken.Hash(rawValue));

        Assert.NotNull(firstToken);

        var storedTokens = await readRepository.GetByFamilyIdAsync(firstToken.FamilyId);

        Assert.Collection(storedTokens.OrderBy(t => t.CreatedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt)
        );
    }
}