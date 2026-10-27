using System.Net;
using System.Net.Http.Json;
using JwtRefresh.Api.IntegrationTests.Base;

namespace JwtRefresh.Api.IntegrationTests.Endpoints;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record RegisterResponse(Guid UserId);
    private sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

    private static string CreateEmail()
        => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_Returns_Created_With_The_User_Id()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
    }

    [Fact]
    public async Task Register_Returns_Conflict_When_The_Email_Is_Taken()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var otherPassword = "otherValidPassword";

        var first = await client.PostAsJsonAsync("/auth/register", new { email, password });

        var second = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password = otherPassword });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "password123")]
    [InlineData("user@example.com", "short")]
    [InlineData("", "password123")]
    public async Task Register_Returns_BadRequest_For_Invalid_Input(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_Returns_Ok_With_The_Token_Pair()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_Returns_Unauthorized_For_A_Wrong_Password()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var wrongPassword = "wrongPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = wrongPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Returns_Unauthorized_For_An_Unknown_Email()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var unknowEmail = CreateEmail();

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { email = unknowEmail, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Returns_The_Same_Response_For_Unknown_Email_And_Wrong_Password()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var unknowEmail = CreateEmail();
        var wrongPassword = "wrongPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var wrongPasswordResponse = await client.PostAsJsonAsync("/auth/login", new { email, password = wrongPassword });
        var unknownEmailResponse  = await client.PostAsJsonAsync("/auth/login", new { email = unknowEmail, password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(wrongPasswordResponse.StatusCode, unknownEmailResponse.StatusCode);
        Assert.Equal(await wrongPasswordResponse.Content.ReadAsStringAsync(), await unknownEmailResponse.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("nao-e-mail", "password123")]
    [InlineData("alguem@exemplo.com", "")]
    public async Task Login_Returns_BadRequest_For_Invalid_Input(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}