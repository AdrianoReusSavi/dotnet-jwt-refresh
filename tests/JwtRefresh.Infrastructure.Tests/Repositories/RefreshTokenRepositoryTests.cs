using JwtRefresh.Domain.Auth;
using JwtRefresh.Domain.Enums;
using JwtRefresh.Infrastructure.Repositories;
using JwtRefresh.Infrastructure.Tests.Base;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JwtRefresh.Infrastructure.Tests.Repositories;

public class RefreshTokenRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private static (RefreshToken Token, string RawValue) IssueToken(Guid? userId = null)
        => RefreshToken.Issue(userId ?? Guid.NewGuid(), Now, Lifetime, SessionLifetime);

    [Fact]
    public async Task Add_And_Commit_Persists_All_Properties()
    {
        await using var context = fixture.CreateContext();
        await using var newContext = fixture.CreateContext();

        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var newRepository = new RefreshTokenRepository(newContext);

        var (token, _) = IssueToken();

        repository.Add(token);
        await unitOfWork.CommitAsync();

        var savedToken = await newRepository.GetByHashAsync(token.TokenHash);

        Assert.NotNull(savedToken);
        Assert.Equal(token.Id, savedToken.Id);
        Assert.Equal(token.UserId, savedToken.UserId);
        Assert.Equal(token.FamilyId, savedToken.FamilyId);
        Assert.Equal(token.TokenHash, savedToken.TokenHash);
        Assert.Equal(token.CreatedAt, savedToken.CreatedAt);
        Assert.Equal(token.ExpiresAt, savedToken.ExpiresAt);
        Assert.Equal(token.SessionExpiresAt, savedToken.SessionExpiresAt);
        Assert.Equal(token.UsedAt, savedToken.UsedAt);
        Assert.Equal(token.RevokedAt, savedToken.RevokedAt);
    }

    [Fact]
    public async Task Rotate_And_Commit_Persists_Used_At_And_Successor_In_Same_Family()
    {
        await using var context = fixture.CreateContext();
        await using var newContext = fixture.CreateContext();

        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var newRepository = new RefreshTokenRepository(newContext);

        var (token, _) = IssueToken();

        repository.Add(token);

        await unitOfWork.CommitAsync();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var result = token.Rotate(rotatedAt, Lifetime);
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.Equal(RotationStatus.Rotated, result.Status);

        repository.Add(newToken);

        await unitOfWork.CommitAsync();

        var firstToken = await newRepository.GetByHashAsync(token.TokenHash);
        var rotatedToken = await newRepository.GetByHashAsync(newToken.TokenHash);

        Assert.NotNull(firstToken);
        Assert.NotNull(rotatedToken);
        Assert.Equal(firstToken.FamilyId, rotatedToken.FamilyId);
        Assert.Equal(rotatedAt, firstToken.UsedAt);
    }

    [Fact]
    public async Task Revoke_And_Commit_Persists_Revoked_At()
    {
        await using var context = fixture.CreateContext();
        await using var newContext = fixture.CreateContext();

        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var newRepository = new RefreshTokenRepository(newContext);

        var (token, _) = IssueToken();

        repository.Add(token);
        await unitOfWork.CommitAsync();

        var revokedAt = Now + TimeSpan.FromDays(1);
        token.Revoke(revokedAt);

        await unitOfWork.CommitAsync();

        var savedToken = await newRepository.GetByHashAsync(token.TokenHash);

        Assert.NotNull(savedToken);
        Assert.Equal(revokedAt, savedToken.RevokedAt);
    }

    [Fact]
    public async Task Duplicated_Hash_Is_Rejected_On_Commit()
    {
        await using var context = fixture.CreateContext();

        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var (token, _) = IssueToken();

        repository.Add(token);
        await unitOfWork.CommitAsync();

        var (newToken, _) = IssueToken();

        repository.Add(newToken);

        context.Entry(newToken)
            .Property(p => p.TokenHash).CurrentValue = token.TokenHash;

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            async () => await unitOfWork.CommitAsync()
        );

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_refresh_tokens_token_hash", postgresException.ConstraintName);
    }

    [Fact]
    public async Task GetByFamilyIdAsync_Returns_Only_Tokens_From_The_Family()
    {
        await using var context = fixture.CreateContext();
        await using var newContext = fixture.CreateContext();

        var repository = new RefreshTokenRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var newRepository = new RefreshTokenRepository(newContext);

        var (token, _) = IssueToken();
        var (anotherToken, _) = IssueToken();

        repository.Add(token);
        repository.Add(anotherToken);

        await unitOfWork.CommitAsync();

        var (secondStatus, secondToken, _) = token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);

        Assert.NotNull(secondToken);
        Assert.Equal(RotationStatus.Rotated, secondStatus);

        repository.Add(secondToken);

        await unitOfWork.CommitAsync();

        var (thirdStatus, thirdToken, _) = secondToken.Rotate(Now + TimeSpan.FromDays(2), Lifetime);

        Assert.NotNull(thirdToken);
        Assert.Equal(RotationStatus.Rotated, thirdStatus);

        repository.Add(thirdToken);

        await unitOfWork.CommitAsync();

        var tokens = await newRepository.GetByFamilyIdAsync(token.FamilyId);

        Assert.Collection(tokens.OrderBy(t => t.CreatedAt),
            item => Assert.Equal(token.Id, item.Id),
            item => Assert.Equal(secondToken.Id, item.Id),
            item => Assert.Equal(thirdToken.Id, item.Id)
        );
        Assert.DoesNotContain(tokens, t => t.Id == anotherToken.Id);
    }

    [Fact]
    public async Task Concurrent_Rotation_Of_The_Same_Token_Is_Rejected_On_Second_Commit()
    {
        await using var setupContext = fixture.CreateContext();
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var setupRepository = new RefreshTokenRepository(setupContext);
        var setupUnitOfWork = new UnitOfWork(setupContext);

        var firstRepository = new RefreshTokenRepository(firstContext);
        var firstUnitOfWork = new UnitOfWork(firstContext);

        var secondRepository = new RefreshTokenRepository(secondContext);
        var secondUnitOfWork = new UnitOfWork(secondContext);

        var readRepository = new RefreshTokenRepository(readContext);

        var (token, _) = IssueToken();

        setupRepository.Add(token);

        await setupUnitOfWork.CommitAsync();

        var firstContextToken = await firstRepository.GetByHashAsync(token.TokenHash);
        var secondContextToken = await secondRepository.GetByHashAsync(token.TokenHash);

        Assert.NotNull(firstContextToken);
        Assert.NotNull(secondContextToken);

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var (firstRotatedStatus, firstRotatedToken, _) = firstContextToken.Rotate(rotatedAt, Lifetime);
        var (secondRotatedStatus, secondRotatedToken, _) = secondContextToken.Rotate(rotatedAt, Lifetime);

        Assert.NotNull(firstRotatedToken);
        Assert.Equal(RotationStatus.Rotated, firstRotatedStatus);
        Assert.NotNull(secondRotatedToken);
        Assert.Equal(RotationStatus.Rotated, secondRotatedStatus);

        firstRepository.Add(firstRotatedToken);
        secondRepository.Add(secondRotatedToken);

        await firstUnitOfWork.CommitAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () => await secondUnitOfWork.CommitAsync()
        );

        var tokens = await readRepository.GetByFamilyIdAsync(token.FamilyId);

        Assert.Collection(tokens.OrderBy(t => t.CreatedAt),
            item => Assert.Equal(token.Id, item.Id),
            item => Assert.Equal(firstRotatedToken.Id, item.Id)
        );
    }

    [Fact]
    public async Task Rotate_Is_Rejected_When_The_Token_Was_Revoked_Concurrently()
    {
        await using var setupContext = fixture.CreateContext();
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var setupRepository = new RefreshTokenRepository(setupContext);
        var setupUnitOfWork = new UnitOfWork(setupContext);

        var firstRepository = new RefreshTokenRepository(firstContext);
        var firstUnitOfWork = new UnitOfWork(firstContext);

        var secondRepository = new RefreshTokenRepository(secondContext);
        var secondUnitOfWork = new UnitOfWork(secondContext);

        var readRepository = new RefreshTokenRepository(readContext);

        var (token, _) = IssueToken();

        setupRepository.Add(token);

        await setupUnitOfWork.CommitAsync();

        var firstContextToken = await firstRepository.GetByHashAsync(token.TokenHash);
        var secondContextToken = await secondRepository.GetByHashAsync(token.TokenHash);

        Assert.NotNull(firstContextToken);
        Assert.NotNull(secondContextToken);

        var revokedAt = Now + TimeSpan.FromDays(1);
        firstContextToken.Revoke(revokedAt);

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var (secondRotatedStatus, secondRotatedToken, _) = secondContextToken.Rotate(rotatedAt, Lifetime);

        Assert.NotNull(secondRotatedToken);
        Assert.Equal(RotationStatus.Rotated, secondRotatedStatus);

        secondRepository.Add(secondRotatedToken);

        await firstUnitOfWork.CommitAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () => await secondUnitOfWork.CommitAsync()
        );

        var tokens = await readRepository.GetByFamilyIdAsync(token.FamilyId);

        var revokedToken = Assert.Single(tokens);

        Assert.Null(revokedToken.UsedAt);
        Assert.Equal(revokedAt, revokedToken.RevokedAt);
    }
}