using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JwtRefresh.Application.Services;

public class RefreshTokenService(
        IRefreshTokenRepository repository,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        IOptions<RefreshTokenOptions> options,
        ILogger<RefreshTokenService> logger
    ) : IRefreshTokenService
{
    private const int MaxRevocationAttempts = 3;

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
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);

            logger.ReuseDetected(token.UserId, token.FamilyId);

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
            logger.RefreshConflict(token.UserId, token.FamilyId);

            return new RefreshResult(RefreshStatus.Conflict, null, null);
        }

        return new RefreshResult(RefreshStatus.Refreshed, result.NextRawValue, token.UserId);
    }

    public async Task RevokeAsync(string rawValue, CancellationToken cancellationToken = default)
    {
        var token = await repository.GetByHashAsync(RefreshToken.Hash(rawValue), cancellationToken);

        if (token is null)
            return;

        await RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);

        logger.SessionEnded(token.UserId, token.FamilyId);
    }

    private static RefreshStatus ToRefreshStatus(RotationStatus status) => status switch
    {
        RotationStatus.Expired => RefreshStatus.Expired,
        RotationStatus.Revoked => RefreshStatus.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var family = await repository.GetByFamilyIdAsync(familyId, cancellationToken);
            foreach (var storedToken in family)
                storedToken.Revoke(now);

            try
            {
                await uow.CommitAsync(cancellationToken);

                return;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxRevocationAttempts)
            {
                uow.DiscardChanges();
            }
        }
    }
}