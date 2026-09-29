using Mammapuls.Api.Auth;

namespace Mammapuls.Api.Tests;

public sealed class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preflight_AllowedOrigin_GetsCorsHeadersWithCredentials()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Preflight(ApiFactory.SpaOrigin), Ct);

        Assert.True(response.IsSuccessStatusCode, $"Status {response.StatusCode}");
        Assert.Equal(ApiFactory.SpaOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Contains("DELETE", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("http://spa.mammapuls.test")]
    [InlineData("https://spa.mammapuls.test.evil.com")]
    [InlineData("null")]
    public async Task Preflight_DisallowedOrigin_GetsNoCorsHeaders(string origin)
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Preflight(origin), Ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task SimpleRequest_DisallowedOrigin_GetsNoAllowOrigin()
    {
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add("Origin", "https://evil.com");

        var response = await client.SendAsync(request, Ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/me");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "DELETE");
        request.Headers.Add("Access-Control-Request-Headers", AuthConstants.CsrfHeaderName.ToLowerInvariant());
        return request;
    }
}
