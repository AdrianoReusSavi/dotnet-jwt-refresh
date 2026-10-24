using JwtRefresh.Domain.Auth;
using JwtRefresh.Infrastructure.Repositories;
using JwtRefresh.Infrastructure.Tests.Base;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JwtRefresh.Infrastructure.Tests.Repositories;

public class UserRepositoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "examplePassword";

    private static string GenerateEmail()
        => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Add_And_Commit_Persists_All_Properties()
    {
        await using var context = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var readRepository = new UserRepository(readContext);

        var email = GenerateEmail();

        var user = User.Register(email, Password, Now);

        repository.Add(user);
        await unitOfWork.CommitAsync();

        var storedUser = await readRepository.GetByEmailAsync(email);

        Assert.NotNull(storedUser);
        Assert.Equal(user.Id, storedUser.Id);
        Assert.Equal(user.Email, storedUser.Email);
        Assert.Equal(user.PasswordHash, storedUser.PasswordHash);
        Assert.Equal(user.CreatedAt, storedUser.CreatedAt);
    }

    [Fact]
    public async Task GetByEmailAsync_Ignores_Case_And_Surrounding_Spaces()
    {
        await using var context = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var readRepository = new UserRepository(readContext);

        var email = GenerateEmail();

        var user = User.Register(email, Password, Now);

        repository.Add(user);
        await unitOfWork.CommitAsync();

        var storedUser = await readRepository.GetByEmailAsync($"  {email.ToUpperInvariant()}  ");

        Assert.NotNull(storedUser);
        Assert.Equal(user.Email, storedUser.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_Returns_Null_For_An_Unknown_Email()
    {
        await using var context = fixture.CreateContext();
        await using var readContext = fixture.CreateContext();

        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var readRepository = new UserRepository(readContext);

        var email = GenerateEmail();

        var user = User.Register(email, Password, Now);

        repository.Add(user);
        await unitOfWork.CommitAsync();

        var storedUser = await readRepository.GetByEmailAsync("  otherEmail@Example.com  ");

        Assert.Null(storedUser);
    }

    [Fact]
    public async Task Duplicated_Email_Is_Rejected_On_Commit()
    {
        await using var context = fixture.CreateContext();
        await using var duplicatedContext = fixture.CreateContext();

        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var duplicatedRepository = new UserRepository(duplicatedContext);
        var duplicatedUnitOfWork = new UnitOfWork(duplicatedContext);

        var email = GenerateEmail();

        var user = User.Register(email, Password, Now);

        repository.Add(user);
        await unitOfWork.CommitAsync();

        var duplicatedUser = User.Register(email.ToUpperInvariant(), Password, Now);

        duplicatedRepository.Add(duplicatedUser);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            async () => await duplicatedUnitOfWork.CommitAsync()
        );

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_users_email", postgresException.ConstraintName);
    }
}