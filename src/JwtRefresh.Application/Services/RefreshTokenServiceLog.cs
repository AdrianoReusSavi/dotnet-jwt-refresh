using Microsoft.Extensions.Logging;

namespace JwtRefresh.Application.Services;

internal static partial class RefreshTokenServiceLog
{
    [LoggerMessage(EventId = 1001, Level =LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; family {FamilyId} revoked")]
    public static partial void ReuseDetected(this ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1002, Level =LogLevel.Information, Message = "Session of user {UserId} ended by logout; family {FamilyId} revoked")]
    public static partial void SessionEnded(this ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1003, Level =LogLevel.Information, Message = "Refresh for user {UserId} lost a concurrent race; family {FamilyId} unchanged")]
    public static partial void RefreshConflict(this ILogger logger, Guid userId, Guid familyId);
}