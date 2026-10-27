namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);