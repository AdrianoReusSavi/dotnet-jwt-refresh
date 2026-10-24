using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;

namespace JwtRefresh.Application.Services;

public class UserService(
        IUserRepository repository,
        IUnitOfWork uow,
        TimeProvider timeProvider
    ) : IUserService
{
    public Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}