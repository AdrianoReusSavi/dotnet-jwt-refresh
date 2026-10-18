using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Infrastructure.Context;

namespace JwtRefresh.Infrastructure.Repositories;

public class UnitOfWork(DataContext context) : IUnitOfWork
{
    private readonly DataContext _context = context;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}