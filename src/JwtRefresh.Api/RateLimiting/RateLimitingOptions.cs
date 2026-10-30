namespace JwtRefresh.Api.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public int PermitLimit { get; init; }
    public TimeSpan Window { get; init; }
}