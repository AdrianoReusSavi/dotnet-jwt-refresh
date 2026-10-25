using System.Net;
using System.Net.Http.Json;
using JwtRefresh.Api.IntegrationTests.Base;

namespace JwtRefresh.Api.IntegrationTests.Endpoints;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record RegisterResponse(Guid UserId);

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
}