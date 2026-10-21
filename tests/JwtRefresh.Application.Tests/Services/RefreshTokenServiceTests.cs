using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
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

    [Fact]
    public async Task RefreshAsync_Returns_NotFound_For_An_Unknown_Token()
    {
        var (_, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.NotFound, result.Status);
        Assert.Null(result.RawValue);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }
    
    [Fact]
    public async Task RefreshAsync_Returns_A_New_Raw_Value_For_The_Same_User()
    {
        var userId = Guid.NewGuid();
        var (token, rawValue) = RefreshToken.Issue(userId, Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Refreshed, result.Status);
        Assert.Equal(userId, result.UserId);
        Assert.NotNull(result.RawValue);
        Assert.NotEqual(rawValue, result.RawValue);
    }

    [Fact]
    public async Task RefreshAsync_Marks_The_Presented_Token_As_Used()
    {
        var userId = Guid.NewGuid();
        var (token, rawValue) = RefreshToken.Issue(userId, Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        var refreshedAt = Now + TimeSpan.FromDays(1);

        _timeProvider.Advance(refreshedAt - Now);

        await _service.RefreshAsync(rawValue);

        var storedToken = await _repository.GetByHashAsync(token.TokenHash);

        Assert.NotNull(storedToken);
        Assert.Equal(refreshedAt, storedToken.UsedAt);
    }

    [Fact]
    public async Task RefreshAsync_Stores_The_Successor_In_The_Same_Family()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        var result = await _service.RefreshAsync(rawValue);

        var tokens = await _repository.GetByFamilyIdAsync(token.FamilyId);

        var successor = Assert.Single(tokens, s => s.Id != token.Id);
        Assert.Equal(RefreshToken.Hash(result.RawValue!), successor.TokenHash);
    }

    [Fact]
    public async Task RefreshAsync_Commits_Once()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        await _service.RefreshAsync(rawValue);

        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RefreshAsync_Returns_Expired_For_An_Expired_Token()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        _timeProvider.Advance(Lifetime + TimeSpan.FromSeconds(1));

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Expired, result.Status);
        Assert.Null(result.RawValue);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RefreshAsync_Returns_Revoked_For_A_Revoked_Token()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        token.Revoke(Now);

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Revoked, result.Status);
        Assert.Null(result.RawValue);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RefreshAsync_Does_Not_Store_A_Successor_When_Refused()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        token.Revoke(Now);

        await _service.RefreshAsync(rawValue);

        var tokens = await _repository.GetByFamilyIdAsync(token.FamilyId);

        var stored = Assert.Single(tokens);
        Assert.Equal(token.Id, stored.Id);
    }
}