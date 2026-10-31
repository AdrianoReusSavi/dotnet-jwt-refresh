using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Services;
using JwtRefresh.Application.Tests.Fakes;
using JwtRefresh.Domain.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
    private readonly FakeLogger<RefreshTokenService> _logger = new();

    private async Task<(RefreshToken First, string FirstRawValue, string SecondRawValue)> IssueAndRotateAsync()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        _timeProvider.Advance(TimeSpan.FromDays(1));

        var tokenRefresh = await _service.RefreshAsync(rawValue);

        _timeProvider.Advance(TimeSpan.FromDays(1));

        return (token, rawValue, tokenRefresh.RawValue!);
    }

    public RefreshTokenServiceTests()
    {
        var options = Options.Create(new RefreshTokenOptions
        {
            Lifetime = Lifetime,
            SessionLifetime = SessionLifetime,
        });

        _service = new RefreshTokenService(_repository, _unitOfWork, _timeProvider, options, _logger);
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

    [Fact]
    public async Task RefreshAsync_Returns_ReuseDetected_When_A_Used_Token_Is_Presented_Again()
    {
        var (_, rawValue, _) = await IssueAndRotateAsync();

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.ReuseDetected, result.Status);
        Assert.Null(result.RawValue);
    }

    [Fact]
    public async Task RefreshAsync_Revokes_The_Whole_Family_On_Reuse()
    {
        var (token, rawValue, _) = await IssueAndRotateAsync();

        await _service.RefreshAsync(rawValue);

        var storedTokens = await _repository.GetByFamilyIdAsync(token.FamilyId);

        Assert.Collection(storedTokens.OrderBy(t => t.CreatedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt)
        );
    }

    [Fact]
    public async Task RefreshAsync_Successor_Cannot_Refresh_After_Reuse()
    {
        var (_, rawValue, secondRawValue) = await IssueAndRotateAsync();

        await _service.RefreshAsync(rawValue);

        var result = await _service.RefreshAsync(secondRawValue);

        Assert.Equal(RefreshStatus.Revoked, result.Status);
    }

    [Fact]
    public async Task RefreshAsync_Does_Not_Revoke_Other_Families_On_Reuse()
    {
        var (_, rawValue, _) = await IssueAndRotateAsync();

        var (otherToken, _) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(otherToken);

        await _service.RefreshAsync(rawValue);

        var storedTokensOtherFamily = await _repository.GetByFamilyIdAsync(otherToken.FamilyId);

        var storedToken = Assert.Single(storedTokensOtherFamily);
        Assert.Null(storedToken.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_Commits_The_Family_Revocation()
    {
        var (_, rawValue, _) = await IssueAndRotateAsync();

        await _service.RefreshAsync(rawValue);

        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RefreshAsync_Returns_Conflict_When_The_Commit_Conflicts()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(token);

        _unitOfWork.PendingConflicts = 1;

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Conflict, result.Status);
        Assert.Null(result.RawValue);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task RefreshAsync_Retries_The_Family_Revocation_On_Conflict()
    {
        var (_, rawValue, _) = await IssueAndRotateAsync();

        _unitOfWork.PendingConflicts = 1;

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.ReuseDetected, result.Status);
        Assert.Equal(2, _unitOfWork.CommitCount);
        Assert.Equal(1, _unitOfWork.DiscardCount);
    }

    [Fact]
    public async Task RefreshAsync_Throws_When_The_Family_Revocation_Keeps_Conflicting()
    {
        var (_, rawValue, _) = await IssueAndRotateAsync();

        _unitOfWork.PendingConflicts = 3;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            async () => await _service.RefreshAsync(rawValue)
        );
    }

    [Fact]
    public async Task RevokeAsync_Revokes_The_Whole_Family()
    {
        var (token, _, secondRawValue) = await IssueAndRotateAsync();

        await _service.RevokeAsync(secondRawValue);

        var storedTokens = await _repository.GetByFamilyIdAsync(token.FamilyId);

        Assert.Collection(storedTokens.OrderBy(t => t.CreatedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt),
            item => Assert.Equal(_timeProvider.GetUtcNow(), item.RevokedAt)
        );
    }

    [Fact]
    public async Task RevokeAsync_Does_Not_Revoke_Other_Families()
    {
        var (_, _, secondRawValue) = await IssueAndRotateAsync();
        var (otherToken, _, _) = await IssueAndRotateAsync();

        await _service.RevokeAsync(secondRawValue);

        var storedOtherTokens = await _repository.GetByFamilyIdAsync(otherToken.FamilyId);

        Assert.Collection(storedOtherTokens.OrderBy(t => t.CreatedAt),
            item => Assert.Null(item.RevokedAt),
            item => Assert.Null(item.RevokedAt)
        );
    }

    [Fact]
    public async Task RevokeAsync_Ignores_An_Unknown_Token()
    {
        var (_, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        await _service.RevokeAsync(rawValue);

        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RevokeAsync_Commits_The_Revocation()
    {
        var (_, _, secondRawValue) = await IssueAndRotateAsync();

        await _service.RevokeAsync(secondRawValue);

        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RefreshAsync_Logs_A_Warning_When_Reuse_Is_Detected()
    {
        var (first, rawValue, _) = await IssueAndRotateAsync();

        await _service.RefreshAsync(rawValue);

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 1001);

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(first.UserId.ToString(), record.GetStructuredStateValue("UserId"));
        Assert.Equal(first.FamilyId.ToString(), record.GetStructuredStateValue("FamilyId"));
    }

    [Fact]
    public async Task RevokeAsync_Logs_The_End_Of_The_Session()
    {
        var (first, _, secondRawValue) = await IssueAndRotateAsync();

        await _service.RevokeAsync(secondRawValue);

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 1002);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(first.UserId.ToString(), record.GetStructuredStateValue("UserId"));
        Assert.Equal(first.FamilyId.ToString(), record.GetStructuredStateValue("FamilyId"));
    }

    [Fact]
    public async Task RefreshAsync_Logs_A_Conflict()
    {
        var (first, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime, SessionLifetime);

        _repository.Add(first);

        _unitOfWork.PendingConflicts = 1;

        var result = await _service.RefreshAsync(rawValue);

        Assert.Equal(RefreshStatus.Conflict, result.Status);

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 1003);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(first.UserId.ToString(), record.GetStructuredStateValue("UserId"));
        Assert.Equal(first.FamilyId.ToString(), record.GetStructuredStateValue("FamilyId"));
    }

    [Fact]
    public async Task RefreshTokenService_Never_Logs_The_Raw_Token()
    {
        var issuedRawValue = await _service.IssueAsync(Guid.NewGuid());
        var (_, firstRawValue, secondRawValue) = await IssueAndRotateAsync();

        await _service.RefreshAsync(firstRawValue);
        await _service.RevokeAsync(issuedRawValue);

        string[] secrets =
        [
            issuedRawValue, firstRawValue, secondRawValue,
            RefreshToken.Hash(issuedRawValue),
            RefreshToken.Hash(firstRawValue),
            RefreshToken.Hash(secondRawValue),
        ];

        var records = _logger.Collector.GetSnapshot();

        Assert.NotEmpty(records);

        foreach (var record in records)
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, record.Message);
            Assert.DoesNotContain(record.StructuredState ?? [], r => r.Value?.Contains(secret) == true);
        }
    }
}