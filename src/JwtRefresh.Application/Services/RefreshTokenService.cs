using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Domain.Enums;
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

        if (result.Next is not { } successor)
            return new RefreshResult(ToRefreshStatus(result.Status), null, null);

        repository.Add(successor);

        await uow.CommitAsync(cancellationToken);

        return new RefreshResult(RefreshStatus.Refreshed, result.NextRawValue, token.UserId);
    }

    private static RefreshStatus ToRefreshStatus(RotationStatus status) => status switch
    {
        RotationStatus.Expired => RefreshStatus.Expired,
        RotationStatus.Revoked => RefreshStatus.Revoked,
        RotationStatus.ReuseDetected => throw new NotImplementedException(),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}