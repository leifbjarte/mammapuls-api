using Mammapuls.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Mammapuls.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Start Vipps login; redirects back to returnUrl (local path or allowed SPA origin)")
            .WithTags("Auth");

        group.MapPost("/auth/logout", (Func<HttpContext, Task<NoContent>>)LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Sign out of the Mammapuls session (does not sign out of Vipps)")
            .WithTags("Auth");

        return group;
    }

    private static async Task<Results<ChallengeHttpResult, ProblemHttpResult>> LoginAsync(
        string? returnUrl,
        IAuthenticationSchemeProvider schemes,
        IOptions<CorsSettings> cors)
    {
        if (await schemes.GetSchemeAsync(AuthConstants.VippsScheme) is null)
        {
            return TypedResults.Problem("Vipps login is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var allowedOrigins = cors.Value.AllowedOrigins;
        returnUrl ??= ReturnUrl.Default(allowedOrigins);
        if (!ReturnUrl.IsAllowed(returnUrl, allowedOrigins))
        {
            return TypedResults.Problem("returnUrl is not allowed.", statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl },
            [AuthConstants.VippsScheme]);
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }
}
