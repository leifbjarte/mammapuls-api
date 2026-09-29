using System.Net;

namespace Mammapuls.Api.Tests;

public sealed class LoginReturnUrlTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/")]
    [InlineData("/profile?tab=1")]
    [InlineData(ApiFactory.SpaOrigin)]
    [InlineData(ApiFactory.SpaOrigin + "/after-login?x=1")]
    public async Task Login_AllowedReturnUrl_ChallengesVipps(string returnUrl)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync($"/api/v1/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(ApiFactory.VippsAuthorizeEndpoint + "?", response.Headers.Location?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_NoReturnUrl_ChallengesVipps()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/v1/auth/login", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(ApiFactory.VippsAuthorizeEndpoint + "?", response.Headers.Location?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("https://evil.com/")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("\\\\evil.com")]
    [InlineData("evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://spa.mammapuls.test")]
    [InlineData("https://spa.mammapuls.test.evil.com")]
    [InlineData("https://spa.mammapuls.test@evil.com")]
    [InlineData("https://user@spa.mammapuls.test/")]
    [InlineData("/ok\r\nSet-Cookie: x=y")]
    public async Task Login_DisallowedReturnUrl_Returns400(string returnUrl)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync($"/api/v1/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
