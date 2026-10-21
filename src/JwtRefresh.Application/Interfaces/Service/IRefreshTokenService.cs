namespace JwtRefresh.Application.Interfaces.Service;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
}