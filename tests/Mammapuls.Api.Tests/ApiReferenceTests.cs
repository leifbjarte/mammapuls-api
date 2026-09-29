using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mammapuls.Api.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mammapuls.Api.Tests;

public sealed class ApiReferenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public async Task OpenApiAndScalar_AreAnonymous_InDevelopmentAndTest(string environment)
    {
        using var client = CreateClient(ForEnvironment(environment));

        var openApi = await client.GetAsync("/openapi/v1.json", Ct);
        var scalar = await client.GetAsync("/scalar/v1", Ct);

        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Equal("text/html", scalar.Content.Headers.ContentType?.MediaType);

        var html = await scalar.Content.ReadAsStringAsync(Ct);
        var scripts = Regex.Matches(html, "<script[^>]+src=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var asset = await client.GetAsync(new Uri(scalar.RequestMessage!.RequestUri!, script), Ct);
            Assert.True(asset.StatusCode == HttpStatusCode.OK, $"{script} -> {asset.StatusCode}");
        }
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task OpenApiAndScalar_AreNotMapped_InProduction(string path)
    {
        var production = ForEnvironment("Production");
        using var client = CreateClient(production);
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, path);
        using var signedIn = new HttpRequestMessage(HttpMethod.Get, path);
        signedIn.Headers.Add("Cookie", CreateSessionCookie(production, factory.Users.Seed().Id));

        var anonymousResponse = await client.SendAsync(anonymous, Ct);
        var signedInResponse = await client.SendAsync(signedIn, Ct);

        // Unmatched routes fall under the authenticated fallback policy, so anonymous callers get 401 before routing reports 404.
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, signedInResponse.StatusCode);
    }

    [Fact]
    public async Task OpenApi_DescribesSessionCookieAndCsrfHeader()
    {
        using var client = factory.CreateApiClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("session");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("cookie", scheme.GetProperty("in").GetString());
        Assert.Equal(AuthConstants.CookieName, scheme.GetProperty("name").GetString());

        var me = paths.GetProperty("/api/v1/me");
        Assert.True(me.GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/ping").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(me.GetProperty("get").TryGetProperty("parameters", out _));

        foreach (var operation in new[] { me.GetProperty("delete"), paths.GetProperty("/api/v1/auth/logout").GetProperty("post") })
        {
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p =>
                p.GetProperty("name").GetString() == AuthConstants.CsrfHeaderName
                && p.GetProperty("in").GetString() == "header"
                && p.GetProperty("required").GetBoolean());
        }
    }

    private WebApplicationFactory<Program> ForEnvironment(string environment) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // Outside Development the API requires a persistent key store; ApiFactory swaps in an in-memory repository.
            builder.UseSetting("ConnectionStrings:dataprotection", "Endpoint=https://localhost:10000/devstoreaccount1;ContainerName=dataprotection");
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // Each derived host has its own key ring, so the cookie must be protected by that host.
    private static string CreateSessionCookie(WebApplicationFactory<Program> app, string userId)
    {
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return $"{AuthConstants.CookieName}={options.TicketDataFormat.Protect(ApiFactory.CreateTicket(userId))}";
    }
}
