using JwtRefresh.Application.Enums;

namespace JwtRefresh.Application.Auth;

public record LoginResult(LoginStatus Status, Guid? UserId);