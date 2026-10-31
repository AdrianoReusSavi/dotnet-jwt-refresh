using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Exceptions;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Domain.Auth;
using Microsoft.Extensions.Logging;

namespace JwtRefresh.Application.Services;

public class UserService(
        IUserRepository repository,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<UserService> logger
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

        try
        {
            await uow.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return new RegisterResult(RegisterStatus.EmailTaken, null);
        }

        return new RegisterResult(RegisterStatus.Registered, user.Id);
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            PasswordHasher.Verify(password, DummyPasswordHash);

            logger.LoginUnknownEmail();

            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        if (!user.VerifyPassword(password))
        {
            logger.LoginWrongPassword(user.Id);

            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        logger.LoginSucceeded(user.Id);

        return new LoginResult(LoginStatus.Succeeded, user.Id);
    }
}