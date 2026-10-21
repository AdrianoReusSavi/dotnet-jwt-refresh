using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Domain.Auth;
using Microsoft.Extensions.Options;

namespace JwtRefresh.Application.Services;

public class RefreshTokenService(
        IRefreshTokenRepository repository,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        IOptions<RefreshTokenOptions> options
    ) : IRefreshTokenService
{
    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (token, rawValue) = RefreshToken.Issue(userId, timeProvider.GetUtcNow(), options.Value.Lifetime, options.Value.SessionLifetime);

        repository.Add(token);

        await uow.CommitAsync(cancellationToken);

        return rawValue;
    }
}