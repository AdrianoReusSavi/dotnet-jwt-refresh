namespace JwtRefresh.Application.Enums;

public enum RefreshStatus
{
    Refreshed,
    NotFound,
    Expired,
    Revoked,
    ReuseDetected,
    Conflict,
}