using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Domain.Tests.Auth;

public class PasswordHasherTests
{
    private const string CorrectPassword = "true_password";
    private const string IncorrectPassword = "false_password";

    [Fact]
    public void Hash_Returns_NonEmpty_String()
    {
        var hash = PasswordHasher.Hash(CorrectPassword);

        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Hash_Returns_Different_Hash_For_Same_Password()
    {
        var firstHash = PasswordHasher.Hash(CorrectPassword);
        var secondHash = PasswordHasher.Hash(CorrectPassword);

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void Verify_Returns_True_For_Correct_Password()
    {
        var hash = PasswordHasher.Hash(CorrectPassword);
        var verify = PasswordHasher.Verify(CorrectPassword, hash);

        Assert.True(verify);
    }

    [Fact]
    public void Verify_Returns_False_For_Incorrect_Password()
    {
        var hash = PasswordHasher.Hash(CorrectPassword);
        var verify = PasswordHasher.Verify(IncorrectPassword, hash);

        Assert.False(verify);
    }
}