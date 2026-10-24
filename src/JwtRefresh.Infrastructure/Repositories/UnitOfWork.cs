using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JwtRefresh.Infrastructure.Repositories;

public class UnitOfWork(DataContext context) : IUnitOfWork
{
    private readonly DataContext _context = context;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The data was modified by another operation since it was loaded.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException("The data violates a unique constraint.", ex);
        }
    }

    public void DiscardChanges()
        => _context.ChangeTracker.Clear();
}