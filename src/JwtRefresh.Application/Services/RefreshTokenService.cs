using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
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

    public async Task<RefreshResult> RefreshAsync(string rawValue, CancellationToken cancellationToken = default)
    {
        var token = await repository.GetByHashAsync(RefreshToken.Hash(rawValue), cancellationToken);

        if (token is null)
            return new RefreshResult(RefreshStatus.NotFound, null, null);

        var result = token.Rotate(timeProvider.GetUtcNow(), options.Value.Lifetime);
        var successor = result.Next;

        repository.Add(successor!);

        await uow.CommitAsync(cancellationToken);

        return new RefreshResult(RefreshStatus.Refreshed, result.NextRawValue, token.UserId);
    }
}