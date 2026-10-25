using System.Net;
using JwtRefresh.Api.IntegrationTests.Base;

namespace JwtRefresh.Api.IntegrationTests;

public class ApiHostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Host_Starts_And_Serves_The_OpenApi_Document()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}