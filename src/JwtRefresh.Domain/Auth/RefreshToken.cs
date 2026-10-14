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

    public static (RefreshToken Token, string RawValue) Issue(Guid userId, DateTimeOffset now, TimeSpan lifetime)
        => throw new NotImplementedException();

    public static string Hash(string rawValue)
        => throw new NotImplementedException();

    public bool IsActive(DateTimeOffset now)
        => throw new NotImplementedException();

    public RotationResult Rotate(DateTimeOffset now, TimeSpan lifetime)
        => throw new NotImplementedException();

    public void Revoke(DateTimeOffset now)
        => throw new NotImplementedException();
}