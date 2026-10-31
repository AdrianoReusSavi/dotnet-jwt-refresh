using Microsoft.Extensions.Logging;

namespace JwtRefresh.Application.Services;

internal static partial class UserServiceLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Login failed: unknown email")]
    public static partial void LoginUnknownEmail(this ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Login failed: wrong password for user {UserId}")]
    public static partial void LoginWrongPassword(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Login succeeded for user {UserId}")]
    public static partial void LoginSucceeded(this ILogger logger, Guid userId);
}