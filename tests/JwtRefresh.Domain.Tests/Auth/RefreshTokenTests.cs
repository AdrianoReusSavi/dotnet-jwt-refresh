using JwtRefresh.Domain.Auth;
using JwtRefresh.Domain.Enums;

namespace JwtRefresh.Domain.Tests.Auth;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private static (RefreshToken Token, string RawValue) IssueToken(Guid? userId = null)
        => RefreshToken.Issue(userId ?? Guid.NewGuid(), Now, Lifetime, SessionLifetime);

    [Fact]
    public void Issue_Stores_Only_The_Hash_Of_The_Raw_Value()
    {
        var (token, rawValue) = IssueToken();

        Assert.NotEqual(rawValue, token.TokenHash);
        Assert.Equal(RefreshToken.Hash(rawValue), token.TokenHash);
    }

    [Fact]
    public void Issue_Returns_Different_Values_On_Each_Call()
    {
        var (token, rawValue) = IssueToken();
        var (newToken, newRawValue) = IssueToken();

        Assert.NotEqual(rawValue, newRawValue);
        Assert.NotEqual(token.TokenHash, newToken.TokenHash);
    }

    [Fact]
    public void Issue_Raw_Value_Has_At_Least_32_Bytes()
    {
        var (_, rawValue) = IssueToken();

        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(rawValue.AsSpan());

        Assert.True(bytes.Length >= 32);
    }

    [Fact]
    public void Issue_Sets_Expiration_From_Lifetime()
    {
        var (token, _) = IssueToken();

        Assert.Equal(Now + Lifetime, token.ExpiresAt);
    }

    [Fact]
    public void Issue_Starts_A_New_Family()
    {
        var userId = Guid.NewGuid();

        var (token, _) = IssueToken(userId);
        var (newToken, _) = IssueToken(userId);

        Assert.NotEqual(token.FamilyId, newToken.FamilyId);
    }

    [Fact]
    public void Issue_Assigns_The_Given_User()
    {
        var userId = Guid.NewGuid();

        var (token, _) = IssueToken(userId);

        Assert.Equal(userId, token.UserId);
    }

    [Fact]
    public void Hash_Returns_Same_Value_For_Same_Input()
    {
        Assert.Equal(RefreshToken.Hash("token"), RefreshToken.Hash("token"));
    }

    [Fact]
    public void Hash_Returns_Different_Values_For_Different_Inputs()
    {
        Assert.NotEqual(RefreshToken.Hash("token-a"), RefreshToken.Hash("token-b"));
    }

    [Fact]
    public void Hash_Does_Not_Contain_The_Raw_Value()
    {
        var rawValue = "token";

        Assert.DoesNotContain(rawValue, RefreshToken.Hash(rawValue));
    }

    [Fact]
    public void IsActive_Returns_True_For_A_New_Token()
    {
        var (token, _) = IssueToken();

        Assert.True(token.IsActive(Now + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void IsActive_Returns_True_Just_Before_Expiration()
    {
        var (token, _) = IssueToken();

        Assert.True(token.IsActive(Now + Lifetime - TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void IsActive_Returns_False_After_Expiration()
    {
        var (token, _) = IssueToken();

        Assert.False(token.IsActive(Now + Lifetime + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void IsActive_Returns_False_At_The_Exact_Expiration_Time()
    {
        var (token, _) = IssueToken();

        Assert.False(token.IsActive(Now + Lifetime));
    }

    [Fact]
    public void IsActive_Returns_False_After_Revoke()
    {
        var (token, _) = IssueToken();

        token.Revoke(Now + TimeSpan.FromDays(1));

        Assert.False(token.IsActive(Now + TimeSpan.FromDays(2)));
    }

    [Fact]
    public void Revoke_Records_When_It_Happened()
    {
        var (token, _) = IssueToken();
        var revokedAt = Now + TimeSpan.FromDays(1);

        token.Revoke(revokedAt);

        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Fact]
    public void Revoke_Twice_Keeps_The_First_Revocation_Time()
    {
        var (token, _) = IssueToken();

        var revokedAt = Now + TimeSpan.FromDays(1);
        var newRevokedAt = Now + TimeSpan.FromDays(2);

        token.Revoke(revokedAt);
        token.Revoke(newRevokedAt);

        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Fact]
    public void Rotate_Returns_Rotated_For_An_Active_Token()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);

        Assert.Equal(RotationStatus.Rotated, result.Status);
    }

    [Fact]
    public void Rotate_Marks_The_Token_As_Used()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        token.Rotate(rotatedAt, Lifetime);

        Assert.Equal(rotatedAt, token.UsedAt);
    }

    [Fact]
    public void Rotate_Does_Not_Revoke_The_Token()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        token.Rotate(rotatedAt, Lifetime);

        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void Rotate_Keeps_The_Same_User_And_Family()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.Equal(token.UserId, newToken.UserId);
        Assert.Equal(token.FamilyId, newToken.FamilyId);
    }

    [Fact]
    public void Rotate_Issues_A_Different_Token()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.NotNull(result.NextRawValue);
        Assert.NotEqual(token.Id, newToken.Id);
        Assert.NotEqual(token.TokenHash, newToken.TokenHash);
        Assert.Equal(RefreshToken.Hash(result.NextRawValue), newToken.TokenHash);
    }

    [Fact]
    public void Rotate_Starts_The_Next_Token_Lifetime_At_Rotation_Time()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var result = token.Rotate(rotatedAt, Lifetime);
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.Equal(rotatedAt, newToken.CreatedAt);
        Assert.Equal(rotatedAt + Lifetime, newToken.ExpiresAt);
    }

    [Fact]
    public void IsActive_Returns_False_After_Rotate()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        token.Rotate(rotatedAt, Lifetime);

        Assert.False(token.IsActive(rotatedAt));
    }

    [Fact]
    public void Rotate_Returns_ReuseDetected_For_A_Used_Token()
    {
        var (token, _) = IssueToken();

        token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);
        var result = token.Rotate(Now + TimeSpan.FromDays(2), Lifetime);

        Assert.Equal(RotationStatus.ReuseDetected, result.Status);
        Assert.Equal(Now + TimeSpan.FromDays(1), token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Rotate_Returns_Expired_For_An_Expired_Token()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + Lifetime + TimeSpan.FromTicks(1);
        var result = token.Rotate(rotatedAt, Lifetime);

        Assert.Equal(RotationStatus.Expired, result.Status);
        Assert.Null(token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Rotate_Returns_Revoked_For_A_Revoked_Token()
    {
        var (token, _) = IssueToken();

        token.Revoke(Now + TimeSpan.FromDays(1));
        var result = token.Rotate(Now + TimeSpan.FromDays(2), Lifetime);

        Assert.Equal(RotationStatus.Revoked, result.Status);
        Assert.Null(token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Rotate_Returns_ReuseDetected_For_A_Used_And_Expired_Token()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var reusedAt = Now + Lifetime + TimeSpan.FromTicks(1);

        token.Rotate(rotatedAt, Lifetime);
        var result = token.Rotate(reusedAt, Lifetime);

        Assert.Equal(RotationStatus.ReuseDetected, result.Status);
        Assert.Equal(rotatedAt, token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Rotate_Returns_ReuseDetected_For_A_Used_And_Revoked_Token()
    {
        var (token, _) = IssueToken();

        var rotatedAt = Now + TimeSpan.FromDays(1);
        var revokedAt = Now + TimeSpan.FromDays(2);
        var reusedAt = Now + TimeSpan.FromDays(3);

        token.Rotate(rotatedAt, Lifetime);
        token.Revoke(revokedAt);

        var result = token.Rotate(reusedAt, Lifetime);

        Assert.Equal(RotationStatus.ReuseDetected, result.Status);
        Assert.Equal(rotatedAt, token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Rotate_Returns_Revoked_For_A_Revoked_And_Expired_Token()
    {
        var (token, _) = IssueToken();

        var revokedAt = Now + TimeSpan.FromDays(2);
        var attemptedAt = Now + Lifetime + TimeSpan.FromTicks(1);

        token.Revoke(revokedAt);

        var result = token.Rotate(attemptedAt, Lifetime);

        Assert.Equal(RotationStatus.Revoked, result.Status);
        Assert.Null(token.UsedAt);
        Assert.Null(result.Next);
        Assert.Null(result.NextRawValue);
    }

    [Fact]
    public void Issue_Sets_Session_Expiration_From_Session_Lifetime()
    {
        var (token, _) = IssueToken();

        Assert.Equal(Now + SessionLifetime, token.SessionExpiresAt);
    }

    [Fact]
    public void Rotate_Keeps_The_Session_Expiration()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), Lifetime);
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.Equal(Now + SessionLifetime, newToken.SessionExpiresAt);
    }

    [Fact]
    public void Rotate_Caps_The_Next_Token_At_The_Session_Expiration()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), TimeSpan.FromDays(60));
        var newToken = result.Next;

        Assert.NotNull(newToken);
        Assert.Equal(Now + SessionLifetime, newToken.ExpiresAt);
    }

    [Fact]
    public void Rotate_Returns_Expired_At_The_Session_Expiration()
    {
        var (token, _) = IssueToken();

        var result = token.Rotate(Now + TimeSpan.FromDays(1), TimeSpan.FromDays(60));
        var next = result.Next;

        Assert.NotNull(next);

        var resultRotated = next.Rotate(Now + SessionLifetime, Lifetime);

        Assert.Equal(RotationStatus.Expired, resultRotated.Status);
        Assert.Null(resultRotated.Next);
        Assert.Null(resultRotated.NextRawValue);
    }
}