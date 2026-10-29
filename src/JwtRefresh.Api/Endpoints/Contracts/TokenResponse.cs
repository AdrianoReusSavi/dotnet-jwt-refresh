namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);