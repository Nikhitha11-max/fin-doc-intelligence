using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinDoc.Tests;

public class ApiAuthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET", "/api/documents")]
    [InlineData("GET", "/api/documents/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/documents/00000000-0000-0000-0000-000000000001/download")]
    [InlineData("PUT", "/api/documents/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/documents/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/documents")]
    public async Task Document_endpoints_require_authentication(string method, string url)
    {
        var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
