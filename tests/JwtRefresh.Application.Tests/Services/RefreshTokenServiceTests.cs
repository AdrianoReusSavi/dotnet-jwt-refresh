using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Services;
using JwtRefresh.Application.Tests.Fakes;
using JwtRefresh.Domain.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JwtRefresh.Application.Tests.Services;

public class RefreshTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private readonly FakeRefreshTokenRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests()
    {
        var options = Options.Create(new RefreshTokenOptions
        {
            Lifetime = Lifetime,
            SessionLifetime = SessionLifetime,
        });

        _service = new RefreshTokenService(_repository, _unitOfWork, _timeProvider, options);
    }

    [Fact]
    public async Task IssueAsync_Returns_A_Raw_Value_Matching_The_Stored_Hash()
    {
        var rawValue = await _service.IssueAsync(Guid.NewGuid());

        var storedToken = Assert.Single(_repository.Tokens);

        Assert.Equal(RefreshToken.Hash(rawValue), storedToken.TokenHash);
    }

    [Fact]
    public async Task IssueAsync_Stores_The_Token_For_The_Given_User()
    {
        var userId = Guid.NewGuid();
        await _service.IssueAsync(userId);

        var storedToken = Assert.Single(_repository.Tokens);

        Assert.Equal(userId, storedToken.UserId);
    }

    [Fact]
    public async Task IssueAsync_Uses_The_Clock_And_The_Configured_Lifetime()
    {
        await _service.IssueAsync(Guid.NewGuid());

        var storedToken = Assert.Single(_repository.Tokens);

        Assert.Equal(Now, storedToken.CreatedAt);
        Assert.Equal(Now + Lifetime, storedToken.ExpiresAt);
        Assert.Equal(Now + SessionLifetime, storedToken.SessionExpiresAt);
    }

    [Fact]
    public async Task IssueAsync_Commits_Once()
    {
        await _service.IssueAsync(Guid.NewGuid());

        Assert.Equal(1, _unitOfWork.CommitCount);
    }
}