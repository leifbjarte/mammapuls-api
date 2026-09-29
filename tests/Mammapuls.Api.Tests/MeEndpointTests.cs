using System.Net;
using System.Net.Http.Json;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace Mammapuls.Api.Tests;

public sealed class MeEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ping_IsAnonymous()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/v1/ping", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PingResponse>(Ct);
        Assert.Equal("pong", body?.Message);
    }

    [Fact]
    public async Task GetMe_Anonymous_Returns401NotRedirect()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task GetMe_Authenticated_ReturnsOwnProfile()
    {
        var user = factory.Users.Seed("Kari Nordmann", "kari@example.test", "4712345678");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/me", factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(Ct);
        Assert.NotNull(me);
        Assert.Equal(user.Id, me.Id);
        Assert.Equal("Kari Nordmann", me.Name);
        Assert.Equal("kari@example.test", me.Email);
        Assert.Equal("4712345678", me.PhoneNumber);
    }

    [Fact]
    public async Task GetMe_TwoUsers_EachSeesOnlyOwnData()
    {
        var userA = factory.Users.Seed("User A", "a@example.test", "4711111111");
        var userB = factory.Users.Seed("User B", "b@example.test", "4722222222");
        using var client = factory.CreateApiClient();

        var meA = await (await SendAsync(client, HttpMethod.Get, "/api/v1/me", factory.CreateSessionCookie(userA.Id)))
            .Content.ReadFromJsonAsync<MeResponse>(Ct);
        var meB = await (await SendAsync(client, HttpMethod.Get, "/api/v1/me", factory.CreateSessionCookie(userB.Id)))
            .Content.ReadFromJsonAsync<MeResponse>(Ct);

        Assert.Equal(new MeResponse(userA.Id, userA.Name, userA.Email, userA.PhoneNumber, userA.CreatedAt), meA);
        Assert.Equal(new MeResponse(userB.Id, userB.Name, userB.Email, userB.PhoneNumber, userB.CreatedAt), meB);
    }

    [Fact]
    public async Task GetMe_CookieNotProtectedByServerKeys_Returns401()
    {
        var victim = factory.Users.Seed("Victim");
        var forged = new TicketDataFormat(new EphemeralDataProtectionProvider().CreateProtector("forged"))
            .Protect(ApiFactory.CreateTicket(victim.Id));
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/me", $"{AuthConstants.CookieName}={forged}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_SessionOlderThanAbsoluteLifetime_Returns401()
    {
        var user = factory.Users.Seed("Stale");
        using var client = factory.CreateApiClient();
        var cookie = factory.CreateSessionCookie(user.Id, authTime: DateTimeOffset.UtcNow.AddHours(-13));

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/me", cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_UserNoLongerInStore_Returns401AndClearsCookie()
    {
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/me", factory.CreateSessionCookie("deleted-user-id"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSessionCookieCleared(response);
    }

    [Fact]
    public async Task DeleteMe_WithoutCsrfHeader_Returns400AndKeepsUser()
    {
        var user = factory.Users.Seed("Keep me");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Delete, "/api/v1/me", factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(factory.Users.Contains(user.Id));
    }

    [Fact]
    public async Task DeleteMe_WithCsrfHeader_DeletesOnlyOwnUserAndClearsCookie()
    {
        var user = factory.Users.Seed("Delete me");
        var other = factory.Users.Seed("Bystander");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Delete, "/api/v1/me", factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(factory.Users.Contains(user.Id));
        Assert.True(factory.Users.Contains(other.Id));
        AssertSessionCookieCleared(response);
    }

    [Fact]
    public async Task DeleteMe_Anonymous_Returns401()
    {
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Delete, "/api/v1/me", cookie: null, csrf: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCsrfHeader_Returns400()
    {
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", cookie: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithCsrfHeader_Returns204AndClearsCookie()
    {
        var user = factory.Users.Seed("Leaving");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/logout", factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertSessionCookieCleared(response);
        Assert.True(factory.Users.Contains(user.Id));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? cookie, bool csrf = false)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (csrf)
        {
            request.Headers.Add(AuthConstants.CsrfHeaderName, "XMLHttpRequest");
        }

        return client.SendAsync(request, Ct);
    }

    private static void AssertSessionCookieCleared(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookies), "Expected a Set-Cookie header.");
        Assert.Contains(setCookies, c =>
            c.StartsWith($"{AuthConstants.CookieName}=;", StringComparison.Ordinal)
            && c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }
}
