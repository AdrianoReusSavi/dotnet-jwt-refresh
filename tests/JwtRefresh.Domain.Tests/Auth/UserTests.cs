using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Domain.Tests.Auth;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Email = "testemail@example.com";
    private const string Password = "examplePassword";

    [Fact]
    public void NormalizeEmail_Trims_And_Lowercases()
    {
        Assert.Equal(Email, User.NormalizeEmail("  TestEmail@Example.com  "));
    }

    [Fact]
    public void Register_Normalizes_The_Email()
    {
        var user = User.Register("  TestEmail@Example.com  ", Password, Now);

        Assert.Equal(Email, user.Email);
    }

    [Fact]
    public void Register_Stores_A_Hash_And_Not_The_Password()
    {
        var user = User.Register(Email, Password, Now);

        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(PasswordHasher.Verify(Password, user.PasswordHash));
    }

    [Fact]
    public void Register_Sets_Id_And_CreatedAt()
    {
        var user = User.Register(Email, Password, Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void VerifyPassword_Returns_True_For_The_Correct_Password()
    {
        var user = User.Register(Email, Password, Now);

        Assert.True(user.VerifyPassword(Password));
    }

    [Fact]
    public void VerifyPassword_Returns_False_For_A_Wrong_Password()
    {
        var user = User.Register(Email, Password, Now);

        var fakePassword = "fakePassword";

        Assert.False(user.VerifyPassword(fakePassword));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Register_Rejects_A_Blank_Email(string email)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(email, Password, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Register_Rejects_A_Blank_Password(string password)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(Email, password, Now));
    }
}