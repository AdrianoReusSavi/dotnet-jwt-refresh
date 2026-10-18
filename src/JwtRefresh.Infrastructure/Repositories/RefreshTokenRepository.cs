using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace JwtRefresh.Infrastructure.Repositories;

public class RefreshTokenRepository(DataContext context) : IRefreshTokenRepository
{
    private readonly DataContext _context = context;

    public void Add(RefreshToken token)
        => _context.RefreshTokens.Add(token);

    public async Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken cancellationToken = default)
        => await _context.RefreshTokens
            .FirstOrDefaultAsync(f => f.TokenHash == hash, cancellationToken);

    public async Task<IReadOnlyCollection<RefreshToken>> GetByFamilyIdAsync(Guid familyId, CancellationToken cancellationToken = default)
        => await _context.RefreshTokens
            .Where(w => w.FamilyId == familyId)
            .ToListAsync(cancellationToken);
}