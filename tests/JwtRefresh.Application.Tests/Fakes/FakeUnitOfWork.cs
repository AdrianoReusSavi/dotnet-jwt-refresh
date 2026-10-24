using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Interfaces.Repository;

namespace JwtRefresh.Application.Tests.Fakes;

public class FakeUnitOfWork : IUnitOfWork
{
    public int CommitCount { get; private set; }
    public int PendingConflicts { get; set; }
    public int PendingUniqueViolations { get; set; }
    public int DiscardCount { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (PendingConflicts > 0)
        {
            PendingConflicts--;

            throw new ConcurrencyConflictException("Simulated concurrency conflict.");
        }

        if (PendingUniqueViolations > 0)
        {
            PendingUniqueViolations--;

            throw new UniqueConstraintViolationException("Simulated unique constraint violation.");
        }

        CommitCount++;

        return Task.CompletedTask;
    }

    public void DiscardChanges()
        => DiscardCount++;
}