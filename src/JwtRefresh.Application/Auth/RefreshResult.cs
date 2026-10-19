using JwtRefresh.Application.Enums;

namespace JwtRefresh.Application.Auth;

public record RefreshResult(RefreshStatus Status, string? RawValue, Guid? UserId);