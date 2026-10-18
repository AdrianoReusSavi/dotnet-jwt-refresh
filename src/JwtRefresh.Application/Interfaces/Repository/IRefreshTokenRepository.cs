using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Application.Interfaces.Repository;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RefreshToken>> GetByFamilyIdAsync(Guid familyId, CancellationToken cancellationToken = default);
}