namespace JwtRefresh.Application.Auth;

public class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";
    public TimeSpan Lifetime { get; init; }
    public TimeSpan SessionLifetime { get; init; }
}