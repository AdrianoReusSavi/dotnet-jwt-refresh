using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Application.Services;

public class UserService(
        IUserRepository repository,
        IUnitOfWork uow,
        TimeProvider timeProvider
    ) : IUserService
{
    public async Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var existingUser = await repository.GetByEmailAsync(email, cancellationToken);

        if (existingUser is not null)
            return new RegisterResult(RegisterStatus.EmailTaken, null);

        var user = User.Register(email, password, timeProvider.GetUtcNow());

        repository.Add(user);

        await uow.CommitAsync(cancellationToken);

        return new RegisterResult(RegisterStatus.Registered, user.Id);
    }

    public Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}