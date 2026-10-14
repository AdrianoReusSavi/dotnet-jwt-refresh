using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace JwtRefresh.Domain.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public Guid FamilyId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken(Guid id, Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, TimeSpan lifetime)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + lifetime;
    }

    public static (RefreshToken Token, string RawValue) Issue(Guid userId, DateTimeOffset now, TimeSpan lifetime)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawValue = Base64Url.EncodeToString(bytes);
        var tokenHash = Hash(rawValue);

        return (new RefreshToken(Guid.NewGuid(), userId, Guid.NewGuid(), tokenHash, now, lifetime), rawValue);
    }

    public static string Hash(string rawValue)
    {
        var bytes = Encoding.UTF8.GetBytes(rawValue);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    public bool IsActive(DateTimeOffset now)
        => throw new NotImplementedException();

    public RotationResult Rotate(DateTimeOffset now, TimeSpan lifetime)
        => throw new NotImplementedException();

    public void Revoke(DateTimeOffset now)
        => throw new NotImplementedException();
}