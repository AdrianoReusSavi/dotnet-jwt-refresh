using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Exceptions;
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

        var now = timeProvider.GetUtcNow();
        var result = token.Rotate(now, options.Value.Lifetime);

        if (result.Status == RotationStatus.ReuseDetected)
        {
            var family = await repository.GetByFamilyIdAsync(token.FamilyId, cancellationToken);
            foreach (var storedToken in family)
                storedToken.Revoke(now);

            await uow.CommitAsync(cancellationToken);

            return new RefreshResult(RefreshStatus.ReuseDetected, null, null);
        }

        if (result.Next is not { } successor)
            return new RefreshResult(ToRefreshStatus(result.Status), null, null);

        repository.Add(successor);

        try
        {
            await uow.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new RefreshResult(RefreshStatus.Conflict, null, null);
        }

        return new RefreshResult(RefreshStatus.Refreshed, result.NextRawValue, token.UserId);
    }

    private static RefreshStatus ToRefreshStatus(RotationStatus status) => status switch
    {
        RotationStatus.Expired => RefreshStatus.Expired,
        RotationStatus.Revoked => RefreshStatus.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}