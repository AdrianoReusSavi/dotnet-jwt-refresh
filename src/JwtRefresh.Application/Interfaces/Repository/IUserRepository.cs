using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Application.Interfaces.Repository;

public interface IUserRepository
{
    void Add(User user);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}