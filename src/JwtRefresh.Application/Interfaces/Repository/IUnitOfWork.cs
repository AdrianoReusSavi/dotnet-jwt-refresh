namespace JwtRefresh.Application.Interfaces.Repository;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    void DiscardChanges();
}