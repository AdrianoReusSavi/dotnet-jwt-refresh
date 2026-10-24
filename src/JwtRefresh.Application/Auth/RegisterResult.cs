using JwtRefresh.Application.Enums;

namespace JwtRefresh.Application.Auth;

public record RegisterResult(RegisterStatus Status, Guid? UserId);