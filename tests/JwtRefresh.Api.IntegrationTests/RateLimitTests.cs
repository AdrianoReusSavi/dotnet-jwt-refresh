using System.Net;
using System.Net.Http.Json;
using JwtRefresh.Api.IntegrationTests.Base;
using Microsoft.AspNetCore.Mvc;

namespace JwtRefresh.Api.IntegrationTests;

public class RateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string CreateEmail()
        => $"{Guid.NewGuid():N}@example.com";

    private HttpClient CreateLimitedClient()
        => factory.WithWebHostBuilder(w => w.UseSetting("RateLimiting:PermitLimit", "2")).CreateClient();

    [Fact]
    public async Task Login_Returns_TooManyRequests_After_The_Permit_Limit()
    {
        var client = CreateLimitedClient();

        var email = CreateEmail();
        var password = "password";

        var firstAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, firstAttempt.StatusCode);

        var secondAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, secondAttempt.StatusCode);

        var thirdAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.TooManyRequests, thirdAttempt.StatusCode);
    }

    [Fact]
    public async Task Register_Returns_TooManyRequests_After_The_Permit_Limit()
    {
        var client = CreateLimitedClient();

        var password = "password";

        var firstAttempt = await client.PostAsJsonAsync("/auth/register", new { email = CreateEmail(), password });

        Assert.Equal(HttpStatusCode.Created, firstAttempt.StatusCode);

        var secondAttempt = await client.PostAsJsonAsync("/auth/register", new { email = CreateEmail(), password });

        Assert.Equal(HttpStatusCode.Created, secondAttempt.StatusCode);

        var thirdAttempt = await client.PostAsJsonAsync("/auth/register", new { email = CreateEmail(), password });

        Assert.Equal(HttpStatusCode.TooManyRequests, thirdAttempt.StatusCode);
    }

    [Fact]
    public async Task Rate_Limited_Response_Is_ProblemDetails_With_Retry_After()
    {
        var client = CreateLimitedClient();

        var email = CreateEmail();
        var password = "password";

        var firstAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, firstAttempt.StatusCode);

        var secondAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, secondAttempt.StatusCode);

        var thirdAttempt = await client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.TooManyRequests, thirdAttempt.StatusCode);
        Assert.Equal("application/problem+json", thirdAttempt.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(thirdAttempt.Headers.RetryAfter);

        var problem = await thirdAttempt.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
    }

    [Fact]
    public async Task Refresh_Is_Not_Rate_Limited()
    {
        var client = CreateLimitedClient();

        var wrongToken = "wrong-token";

        var firstAttempt = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.Unauthorized, firstAttempt.StatusCode);

        var secondAttempt = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.Unauthorized, secondAttempt.StatusCode);

        var thirdAttempt = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = wrongToken });

        Assert.Equal(HttpStatusCode.Unauthorized, thirdAttempt.StatusCode);
    }
}