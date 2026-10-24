using JwtRefresh.Application.Interfaces.Repository;

namespace JwtRefresh.Infrastructure.Tests.Base;

public sealed class InterferingUnitOfWork(IUnitOfWork inner, Func<Task> interference) : IUnitOfWork
{
    public int InterferenceCount { get; private set; }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (InterferenceCount is 0)
        {
            InterferenceCount++;

            await interference();
        }

        await inner.CommitAsync(cancellationToken);
    }

    public void DiscardChanges()
        => inner.DiscardChanges();
}