using System.Globalization;
using System.Security.Claims;
using Azure.Storage.Blobs;
using Mammapuls.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace Mammapuls.Api.Auth;

public static class AuthExtensions
{
    private static readonly TimeSpan SlidingLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    // Vipps scope names; the returned claims are name, email and phone_number.
    private static readonly string[] VippsScopes = ["openid", "name", "email", "phoneNumber"];

    public static WebApplicationBuilder AddMammapulsAuth(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Container Apps ingress has no fixed proxy IP, and the app is only reachable through it.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        AddDataProtection(builder);
        AddCors(builder);

        var vipps = builder.Configuration.GetSection(VippsOptions.SectionName).Get<VippsOptions>() ?? new();
        var authentication = builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(ConfigureCookie);

        // Without credentials the API still runs; /auth/login then returns 503.
        if (vipps.IsConfigured)
        {
            authentication.AddOpenIdConnect(AuthConstants.VippsScheme, "Vipps", options => ConfigureVipps(options, vipps));
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }

    public static WebApplication UseMammapulsAuth(this WebApplication app)
    {
        app.UseCors();
        app.Use(RequireCsrfHeaderOnUnsafeMethods);
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(AuthConstants.UserIdClaim)
        ?? throw new InvalidOperationException("Authenticated principal has no user id claim.");

    private static void AddDataProtection(WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Mammapuls.Api");

        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(AuthConstants.DataProtectionConnectionName)))
        {
            builder.AddKeyedAzureBlobContainerClient(AuthConstants.DataProtectionConnectionName);
            dataProtection.PersistKeysToAzureBlobStorage(services => services
                .GetRequiredKeyedService<BlobContainerClient>(AuthConstants.DataProtectionConnectionName)
                .GetBlobClient("keys.xml"));
        }
        else if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{AuthConstants.DataProtectionConnectionName} is required outside Development so sessions survive restarts.");
        }
    }

    private static void AddCors(WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(CorsSettings.SectionName);
        builder.Services.Configure<CorsSettings>(section);
        var allowedOrigins = section.Get<CorsSettings>()?.AllowedOrigins ?? [];

        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
            .WithHeaders(HeaderNames.ContentType, AuthConstants.CsrfHeaderName)
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = AuthConstants.CookieName;
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = SlidingLifetime;
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var authTime = context.Principal?.FindFirstValue(AuthConstants.AuthTimeClaim);
            if (!long.TryParse(authTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                || DateTimeOffset.FromUnixTimeSeconds(seconds) + AbsoluteLifetime < DateTimeOffset.UtcNow)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    }

    private static void ConfigureVipps(OpenIdConnectOptions options, VippsOptions vipps)
    {
        options.Authority = vipps.Authority;
        options.ClientId = vipps.ClientId;
        options.ClientSecret = vipps.ClientSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = AuthConstants.CallbackPath;

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.UsePkce = true;
        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
        options.SaveTokens = false;
        options.MapInboundClaims = false;
        // "ID token includes userinfo" stays off in the Vipps portal, so profile claims come from userinfo.
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        foreach (var scope in VippsScopes)
        {
            options.Scope.Add(scope);
        }

        options.ClaimActions.MapUniqueJsonKey("phone_number", "phone_number");
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        options.TokenValidationParameters.NameClaimType = "name";

        options.Events.OnAuthorizationCodeReceived = VippsTokenClient.RedeemWithClientSecretBasicAsync;
        options.Events.OnTicketReceived = OnVippsTicketReceivedAsync;
        options.Events.OnRemoteFailure = OnVippsRemoteFailure;
    }

    // Runs after userinfo has been merged (OnTokenValidated fires before the userinfo call).
    private static async Task OnVippsTicketReceivedAsync(TicketReceivedContext context)
    {
        var principal = context.Principal ?? throw new InvalidOperationException("Vipps login produced no principal.");
        var subject = principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Vipps ID token has no sub claim.");
        var cancellationToken = context.HttpContext.RequestAborted;

        var options = (OpenIdConnectOptions)context.Options;
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);

        var services = context.HttpContext.RequestServices;
        var user = await services.GetRequiredService<IUserStore>().UpsertFromLoginAsync(
            configuration.Issuer,
            subject,
            principal.FindFirstValue("name"),
            principal.FindFirstValue("email"),
            principal.FindFirstValue("phone_number"),
            cancellationToken);

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AuthConstants.UserIdClaim, user.Id),
                new Claim(
                    AuthConstants.AuthTimeClaim,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthConstants.UserIdClaim,
            roleType: null));

        services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthExtensions))
            .LogInformation("User {UserId} signed in with Vipps", user.Id);
    }

    private static Task OnVippsRemoteFailure(RemoteFailureContext context)
    {
        var services = context.HttpContext.RequestServices;
        services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthExtensions))
            .LogWarning("Vipps login failed: {Reason}", context.Failure?.Message);

        var allowedOrigins = services.GetRequiredService<IOptions<CorsSettings>>().Value.AllowedOrigins;
        var target = context.Properties?.RedirectUri;
        if (!ReturnUrl.IsAllowed(target, allowedOrigins))
        {
            target = ReturnUrl.Default(allowedOrigins);
        }

        context.Response.Redirect(QueryHelpers.AddQueryString(target!, "login", "failed"));
        context.HandleResponse();
        return Task.CompletedTask;
    }

    // Custom header forces a CORS preflight, which only allowlisted origins pass; HTML forms can't set it.
    private static Task RequireCsrfHeaderOnUnsafeMethods(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)
            || !string.IsNullOrEmpty(context.Request.Headers[AuthConstants.CsrfHeaderName]))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Task.CompletedTask;
    }
}
