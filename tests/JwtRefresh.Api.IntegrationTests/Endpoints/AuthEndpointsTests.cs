using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JwtRefresh.Api.IntegrationTests.Base;
using JwtRefresh.Application.Auth;
using Microsoft.AspNetCore.Mvc;
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
    private sealed record RefreshResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

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

    private static async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginAsync(HttpClient client)
    {
        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var registerBody = await register.Content.ReadFromJsonAsync<RegisterResponse>();

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>();

        return (registerBody!.UserId, tokens!);
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

        var wrongPasswordProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var unknownEmailProblem = await unknownEmailResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(wrongPasswordProblem);
        Assert.NotNull(unknownEmailProblem);

        Assert.Equal(wrongPasswordProblem.Status, unknownEmailProblem.Status);
        Assert.Equal(wrongPasswordProblem.Type, unknownEmailProblem.Type);
        Assert.Equal(wrongPasswordProblem.Title, unknownEmailProblem.Title);
        Assert.Equal(wrongPasswordProblem.Detail, unknownEmailProblem.Detail);
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

    [Fact]
    public async Task Refresh_Returns_Ok_With_A_New_Token_Pair()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var result = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadFromJsonAsync<RefreshResponse>();

        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.NotEqual(tokens.RefreshToken, body.RefreshToken);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_Returns_An_Access_Token_For_The_Same_User()
    {
        var client = factory.CreateClient();

        var (userId, tokens) = await RegisterAndLoginAsync(client);

        var result = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadFromJsonAsync<RefreshResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        var me = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var bodyMe = await me.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(userId, bodyMe!.UserId);
    }

    [Fact]
    public async Task Refresh_Returns_Unauthorized_For_An_Unknown_Token()
    {
        var client = factory.CreateClient();

        var wrongToken = "wrong-token";

        var result = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Refresh_Returns_Unauthorized_When_A_Rotated_Token_Is_Reused()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var first = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var reuse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_Revokes_The_Successor_When_A_Rotated_Token_Is_Reused()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var successor = await refresh.Content.ReadFromJsonAsync<RefreshResponse>();

        var reuse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var afterReuse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = successor!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_Returns_BadRequest_For_An_Empty_Token()
    {
        var client = factory.CreateClient();

        var result = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Logout_Returns_NoContent()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Logout_Revokes_The_Refresh_Token()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_With_A_Rotated_Token_Revokes_The_Current_One()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var bodyRefresh = await refresh.Content.ReadFromJsonAsync<RefreshResponse>();

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = bodyRefresh!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterRefresh.StatusCode);
    }

    [Fact]
    public async Task Logout_Does_Not_Revoke_Other_Sessions()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var firstLogin = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);

        var bodyFirstLogin = await firstLogin.Content.ReadFromJsonAsync<LoginResponse>();

        var secondLogin = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);

        var bodySecondLogin = await secondLogin.Content.ReadFromJsonAsync<LoginResponse>();

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = bodyFirstLogin!.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = bodySecondLogin!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_Returns_NoContent_For_An_Unknown_Token()
    {
        var client = factory.CreateClient();

        var wrongToken = "wrong-token";

        var result = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Fact]
    public async Task Logout_Does_Not_Invalidate_The_Access_Token()
    {
        var client = factory.CreateClient();

        var (_, tokens) = await RegisterAndLoginAsync(client);

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task Logout_Returns_BadRequest_For_An_Empty_Token()
    {
        var client = factory.CreateClient();

        var result = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Login_Returns_ProblemDetails_For_Invalid_Credentials()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var wrongPassword = "wrongPassword";

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = wrongPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public async Task Register_Returns_ProblemDetails_When_The_Email_Is_Taken()
    {
        var client = factory.CreateClient();

        var email = CreateEmail();
        var password = "validPassword";
        var otherPassword = "otherValidPassword";

        var first = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password = otherPassword });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Conflict", problem.Title);
    }

    [Fact]
    public async Task Refresh_Returns_ProblemDetails_For_A_Refused_Token()
    {
        var client = factory.CreateClient();

        var wrongToken = "wrong-token";

        var result = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("application/problem+json", result.Content.Headers.ContentType?.MediaType);

        var problem = await result.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public async Task Me_Returns_ProblemDetails_Without_A_Token()
    {
        var client = factory.CreateClient();

        var result = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("application/problem+json", result.Content.Headers.ContentType?.MediaType);

        var problem = await result.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
    }
}