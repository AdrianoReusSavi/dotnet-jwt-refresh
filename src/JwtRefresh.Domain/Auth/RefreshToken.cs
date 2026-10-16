using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using JwtRefresh.Domain.Enums;

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
        var (rawValue, tokenHash) = GenerateRawValue();

        return (new RefreshToken(Guid.NewGuid(), userId, Guid.NewGuid(), tokenHash, now, lifetime), rawValue);
    }

    public static string Hash(string rawValue)
    {
        var bytes = Encoding.UTF8.GetBytes(rawValue);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    public bool IsActive(DateTimeOffset now)
        => !IsExpired(now) && !IsUsed() && !IsRevoked();

    private bool IsExpired(DateTimeOffset now)
        => ExpiresAt <= now;

    private bool IsUsed()
        => UsedAt is not null;

    private bool IsRevoked()
        => RevokedAt is not null;

    public RotationResult Rotate(DateTimeOffset now, TimeSpan lifetime)
    {
        if (IsUsed())
            return new RotationResult(RotationStatus.ReuseDetected, null, null);

        if (IsRevoked())
            return new RotationResult(RotationStatus.Revoked, null, null);

        if (IsExpired(now))
            return new RotationResult(RotationStatus.Expired, null, null);

        UsedAt = now;

        var (rawValue, tokenHash) = GenerateRawValue();

        var next = new RefreshToken(Guid.NewGuid(), UserId, FamilyId, tokenHash, now, lifetime);

        return new RotationResult(RotationStatus.Rotated, next, rawValue);
    }

    public void Revoke(DateTimeOffset now)
        => RevokedAt ??= now;

    private static (string RawValue, string TokenHash) GenerateRawValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawValue = Base64Url.EncodeToString(bytes);
        return (rawValue, Hash(rawValue));
    }
}