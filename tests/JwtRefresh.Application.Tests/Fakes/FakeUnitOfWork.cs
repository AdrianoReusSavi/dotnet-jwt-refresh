using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Interfaces.Repository;

namespace JwtRefresh.Application.Tests.Fakes;

public class FakeUnitOfWork : IUnitOfWork
{
    public int CommitCount { get; private set; }
    public bool ConflictOnNextCommit { get; set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (ConflictOnNextCommit)
        {
            ConflictOnNextCommit = false;

            throw new ConcurrencyConflictException("Simulated concurrency conflict.");
        }

        CommitCount++;

        return Task.CompletedTask;
    }
}