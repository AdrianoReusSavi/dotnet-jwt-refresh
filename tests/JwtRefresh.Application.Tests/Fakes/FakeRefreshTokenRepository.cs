using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Application.Tests.Fakes;

public class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public void Add(RefreshToken token)
        => Tokens.Add(token);

    public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken cancellationToken = default)
        => Task.FromResult(Tokens.FirstOrDefault(f => f.TokenHash == hash));

    public Task<IReadOnlyCollection<RefreshToken>> GetByFamilyIdAsync(Guid familyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RefreshToken>>(Tokens.Where(w => w.FamilyId == familyId).ToList());
}