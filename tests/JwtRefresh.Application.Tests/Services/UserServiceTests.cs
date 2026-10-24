using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Services;
using JwtRefresh.Application.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace JwtRefresh.Application.Tests.Services;

public class UserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "examplePassword";

    private readonly FakeUserRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly UserService _service;

    public UserServiceTests()
        => _service = new UserService(_repository, _unitOfWork, _timeProvider);

    private static string GenerateEmail()
        => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task RegisterAsync_Stores_The_User_And_Returns_Its_Id()
    {
        var register = await _service.RegisterAsync(GenerateEmail(), Password);

        var user = Assert.Single(_repository.Users);

        Assert.Equal(RegisterStatus.Registered, register.Status);
        Assert.Equal(user.Id, register.UserId);
    }

    [Fact]
    public async Task RegisterAsync_Commits_The_User()
    {
        await _service.RegisterAsync(GenerateEmail(), Password);

        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RegisterAsync_Returns_EmailTaken_For_An_Existing_Email()
    {
        var email = GenerateEmail();
        var emailVariant = $"  {email.ToUpperInvariant()}  ";

        await _service.RegisterAsync(email, Password);

        var register = await _service.RegisterAsync(emailVariant, Password);

        Assert.Equal(RegisterStatus.EmailTaken, register.Status);
        Assert.Null(register.UserId);

        Assert.Single(_repository.Users);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }
}