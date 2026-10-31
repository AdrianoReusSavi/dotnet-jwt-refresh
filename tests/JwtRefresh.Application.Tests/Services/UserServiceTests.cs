using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Services;
using JwtRefresh.Application.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace JwtRefresh.Application.Tests.Services;

public class UserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "examplePassword";

    private readonly FakeUserRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly FakeLogger<UserService> _logger = new();
    private readonly UserService _service;

    public UserServiceTests()
        => _service = new UserService(_repository, _unitOfWork, _timeProvider, _logger);

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

    [Fact]
    public async Task RegisterAsync_Returns_EmailTaken_When_The_Email_Is_Taken_Concurrently()
    {
        var email = GenerateEmail();

        _unitOfWork.PendingUniqueViolations = 1;

        var result = await _service.RegisterAsync(email, Password);

        Assert.Equal(new RegisterResult(RegisterStatus.EmailTaken, null), result);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task LoginAsync_Succeeds_With_The_Correct_Password()
    {
        var email = GenerateEmail();

        await _service.RegisterAsync(email, Password);

        var loginResult = await _service.LoginAsync(email, Password);

        var user = Assert.Single(_repository.Users);

        Assert.Equal(LoginStatus.Succeeded, loginResult.Status);
        Assert.Equal(user.Id, loginResult.UserId);
    }

    [Fact]
    public async Task LoginAsync_Ignores_Email_Case_And_Spaces()
    {
        var email = GenerateEmail();
        var emailVariant = $"  {email.ToUpperInvariant()}  ";

        await _service.RegisterAsync(email, Password);

        var loginResult = await _service.LoginAsync(emailVariant, Password);

        var user = Assert.Single(_repository.Users);

        Assert.Equal(LoginStatus.Succeeded, loginResult.Status);
        Assert.Equal(user.Id, loginResult.UserId);
    }

    [Fact]
    public async Task LoginAsync_Rejects_A_Wrong_Password()
    {
        var email = GenerateEmail();

        await _service.RegisterAsync(email, Password);

        var result = await _service.LoginAsync(email, "wrongPassword");

        Assert.Equal(new LoginResult(LoginStatus.InvalidCredentials, null), result);
    }

    [Fact]
    public async Task LoginAsync_Rejects_An_Unknown_Email()
    {
        await _service.RegisterAsync(GenerateEmail(), Password);

        var result = await _service.LoginAsync("wrongEmail@example.com", Password);

        Assert.Equal(new LoginResult(LoginStatus.InvalidCredentials, null), result);
    }

    [Fact]
    public async Task LoginAsync_Does_Not_Commit()
    {
        var email = GenerateEmail();

        await _service.RegisterAsync(email, Password);

        var commitsBefore = _unitOfWork.CommitCount;

        await _service.LoginAsync(email, Password);

        Assert.Equal(commitsBefore, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task LoginAsync_Logs_A_Warning_For_An_Unknown_Email()
    {
        await _service.LoginAsync(GenerateEmail(), Password);

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 2001);

        Assert.Equal(LogLevel.Warning, record.Level);

        var keys = (record.StructuredState ?? []).Select(s => s.Key);
        Assert.Equal(["{OriginalFormat}"], keys);
    }

    [Fact]
    public async Task LoginAsync_Logs_A_Warning_For_A_Wrong_Password()
    {
        var email = GenerateEmail();

        var register = await _service.RegisterAsync(email, Password);

        await _service.LoginAsync(email, "wrongPassword");

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 2002);

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(register.UserId.ToString(), record.GetStructuredStateValue("UserId"));
    }

    [Fact]
    public async Task LoginAsync_Logs_A_Successful_Login()
    {
        var email = GenerateEmail();

        var register = await _service.RegisterAsync(email, Password);

        await _service.LoginAsync(email, Password);

        var record = Assert.Single(_logger.Collector.GetSnapshot(), s => s.Id.Id == 2003);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(register.UserId.ToString(), record.GetStructuredStateValue("UserId"));
    }

    [Fact]
    public async Task UserService_Never_Logs_The_Email_Or_The_Password()
    {
        var email = $"User.{Guid.NewGuid():N}@Example.com";
        var unknownEmail = $"Other.{Guid.NewGuid():N}@Example.com";
        var wrongPassword = "wrongPassword";

        await _service.RegisterAsync(email, Password);
        await _service.LoginAsync(email, Password);
        await _service.LoginAsync(email, wrongPassword);
        await _service.LoginAsync(unknownEmail, Password);

        string[] secrets =
        [
            email, email.ToLowerInvariant(),
            unknownEmail, unknownEmail.ToLowerInvariant(),
            Password, wrongPassword,
        ];

        var records = _logger.Collector.GetSnapshot();

        Assert.NotEmpty(records);

        foreach (var record in records)
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, record.Message);
            Assert.DoesNotContain(record.StructuredState ?? [], r => r.Value?.Contains(secret) == true);
        }
    }
}