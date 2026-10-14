using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Domain.Tests.Auth;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Issue_Stores_Only_The_Hash_Of_The_Raw_Value()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime);

        Assert.NotEqual(rawValue, token.TokenHash);
        Assert.Equal(RefreshToken.Hash(rawValue), token.TokenHash);
    }

    [Fact]
    public void Issue_Returns_Different_Values_On_Each_Call()
    {
        var (token, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime);
        var (newToken, newRawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime);

        Assert.NotEqual(rawValue, newRawValue);
        Assert.NotEqual(token.TokenHash, newToken.TokenHash);
    }

    [Fact]
    public void Issue_Raw_Value_Has_At_Least_32_Bytes()
    {
        var (_, rawValue) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime);

        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(rawValue.AsSpan());

        Assert.True(bytes.Length >= 32);
    }

    [Fact]
    public void Issue_Sets_Expiration_From_Lifetime()
    {
        var (token, _) = RefreshToken.Issue(Guid.NewGuid(), Now, Lifetime);

        Assert.Equal(Now + Lifetime, token.ExpiresAt);
    }

    [Fact]
    public void Issue_Starts_A_New_Family()
    {
        var id = Guid.NewGuid();

        var (token, _) = RefreshToken.Issue(id, Now, Lifetime);
        var (newToken, _) = RefreshToken.Issue(id, Now, Lifetime);

        Assert.NotEqual(token.FamilyId, newToken.FamilyId);
    }

    [Fact]
    public void Issue_Assigns_The_Given_User()
    {
        var userId = Guid.NewGuid();

        var (token, _) = RefreshToken.Issue(userId, Now, Lifetime);

        Assert.Equal(userId, token.UserId);
    }
}