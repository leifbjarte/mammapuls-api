using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Xml.Linq;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mammapuls.Api.Tests;

/// <summary>Runs the real API pipeline fully offline: no Azure, no emulators, no Vipps.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SpaOrigin = "https://spa.mammapuls.test";
    public const string VippsIssuer = "https://vipps.test/access-management-1.0/access/";
    public const string VippsAuthorizeEndpoint = "https://vipps.test/access-management-1.0/access/oauth2/auth";

    public InMemoryUserStore Users { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Aspire clients are only constructed on first resolve; IUserStore is faked, so they never are.
        builder.UseSetting("ConnectionStrings:users", "AccountEndpoint=https://localhost:8081/;Database=mammapuls;Container=users");
        builder.UseSetting("ConnectionStrings:media", "Endpoint=https://localhost:10000/devstoreaccount1;ContainerName=media");
        builder.UseSetting("ConnectionStrings:dataprotection", "");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");

        builder.UseSetting("Authentication:Vipps:Authority", VippsIssuer);
        builder.UseSetting("Authentication:Vipps:ClientId", "test-client");
        builder.UseSetting("Authentication:Vipps:ClientSecret", "test-secret");
        builder.UseSetting("Cors:AllowedOrigins:0", SpaOrigin);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserStore>();
            services.AddSingleton<IUserStore>(Users);

            services.Configure<KeyManagementOptions>(options => options.XmlRepository = new InMemoryXmlRepository());

            // Static metadata stops the OIDC handler from fetching the discovery document.
            services.Configure<OpenIdConnectOptions>(AuthConstants.VippsScheme, options =>
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = VippsIssuer,
                    AuthorizationEndpoint = VippsAuthorizeEndpoint,
                    TokenEndpoint = VippsIssuer + "oauth2/token",
                });
        });
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    /// <summary>Builds a session cookie exactly as the Vipps callback would, using the app's own data protection.</summary>
    public string CreateSessionCookie(string userId, DateTimeOffset? authTime = null)
    {
        var options = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return $"{AuthConstants.CookieName}={options.TicketDataFormat.Protect(CreateTicket(userId, authTime))}";
    }

    public static AuthenticationTicket CreateTicket(string userId, DateTimeOffset? authTime = null)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthConstants.UserIdClaim, userId),
                new Claim(
                    AuthConstants.AuthTimeClaim,
                    (authTime ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthConstants.UserIdClaim,
            roleType: null);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<string, AppUser> users = new();

    public AppUser Seed(string? name = null, string? email = null, string? phoneNumber = null)
    {
        var subject = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var user = new AppUser(CosmosUserStore.CreateId(ApiFactory.VippsIssuer, subject), ApiFactory.VippsIssuer, subject, name, email, phoneNumber, now, now);
        users[user.Id] = user;
        return user;
    }

    public bool Contains(string id) => users.ContainsKey(id);

    public Task<AppUser> UpsertFromLoginAsync(string issuer, string subject, string? name, string? email, string? phoneNumber, CancellationToken cancellationToken)
    {
        var id = CosmosUserStore.CreateId(issuer, subject);
        var now = DateTimeOffset.UtcNow;
        var user = users.AddOrUpdate(
            id,
            _ => new AppUser(id, issuer, subject, name, email, phoneNumber, now, now),
            (_, existing) => existing with { Name = name, Email = email, PhoneNumber = phoneNumber, UpdatedAt = now });
        return Task.FromResult(user);
    }

    public Task<AppUser?> GetAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(users.TryGetValue(id, out var user) ? user : null);

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        users.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (elements)
        {
            return [.. elements];
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (elements)
        {
            elements.Add(element);
        }
    }
}
