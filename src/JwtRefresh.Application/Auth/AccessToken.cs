namespace JwtRefresh.Application.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAt);