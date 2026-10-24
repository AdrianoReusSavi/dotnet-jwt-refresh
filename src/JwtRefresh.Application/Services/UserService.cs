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
    // Verified when the email is unknown, so both failure paths spend the same BCrypt time
    // and the response time does not reveal which emails are registered.
    private static readonly string DummyPasswordHash = PasswordHasher.Hash("dummy-password-for-timing");

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

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            PasswordHasher.Verify(password, DummyPasswordHash);

            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        if (!user.VerifyPassword(password))
            return new LoginResult(LoginStatus.InvalidCredentials, null);

        return new LoginResult(LoginStatus.Succeeded, user.Id);
    }
}