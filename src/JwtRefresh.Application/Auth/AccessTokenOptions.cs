namespace JwtRefresh.Application.Auth;

public class AccessTokenOptions
{
    public const string SectionName = "AccessToken";
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public TimeSpan Lifetime { get; init; }
}