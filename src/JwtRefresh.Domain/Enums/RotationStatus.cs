namespace JwtRefresh.Domain.Enums;

public enum RotationStatus
{
    Rotated,
    ReuseDetected,
    Expired,
    Revoked,
}