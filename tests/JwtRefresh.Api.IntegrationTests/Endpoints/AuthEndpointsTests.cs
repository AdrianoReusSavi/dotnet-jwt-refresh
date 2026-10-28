using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JwtRefresh.Api.IntegrationTests.Base;
using JwtRefresh.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JwtRefresh.Api.IntegrationTests.Endpoints;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record RegisterResponse(Guid UserId);
    private sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
    private sealed record MeResponse(Guid UserId);

    private static string CreateEmail()
        => $"{Guid.NewGuid():N}@example.com";

    private string CreateToken(Guid userId, string signingKey, DateTimeOffset expiresAt)
    {
        var options = factory.Services.GetRequiredService<IOptions<AccessTokenOptions>>().Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var issuedAt = expiresAt - options.Lifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

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

    [Fact]
    public async Task Me_Returns_The_User_Id_For_A_Valid_Access_Token()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var bodyRegister = await register.Content.ReadFromJsonAsync<RegisterResponse>();

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var bodyLogin = await login.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bodyLogin!.AccessToken);

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadFromJsonAsync<MeResponse>();

        Assert.NotNull(body);
        Assert.Equal(bodyRegister!.UserId, body.UserId);
    }

    [Fact]
    public async Task Me_Returns_Unauthorized_Without_A_Token()
    {
        var client = factory.CreateClient();

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Me_Returns_Unauthorized_For_A_Malformed_Token()
    {
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Me_Returns_Unauthorized_For_A_Token_Signed_With_Another_Key()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var bodyRegister = await register.Content.ReadFromJsonAsync<RegisterResponse>();

        var token = CreateToken(bodyRegister!.UserId, "another-signing-key-with-32-char", DateTimeOffset.UtcNow.AddMinutes(15));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Me_Returns_Unauthorized_For_An_Expired_Token()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var bodyRegister = await register.Content.ReadFromJsonAsync<RegisterResponse>();

        var signingKey = factory.Services.GetRequiredService<IOptions<AccessTokenOptions>>().Value.SigningKey;
        var token = CreateToken(bodyRegister!.UserId, signingKey, DateTimeOffset.UtcNow.AddMinutes(-5));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }
}