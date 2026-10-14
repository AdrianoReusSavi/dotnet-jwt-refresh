using JwtRefresh.Domain.Enums;

namespace JwtRefresh.Domain.Auth;

public record RotationResult(RotationStatus Status, RefreshToken? Next, string? NextRawValue);