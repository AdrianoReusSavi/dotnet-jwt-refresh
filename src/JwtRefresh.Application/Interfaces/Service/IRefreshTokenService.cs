using JwtRefresh.Application.Auth;

namespace JwtRefresh.Application.Interfaces.Service;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RefreshResult> RefreshAsync(string rawValue, CancellationToken cancellationToken = default);
}