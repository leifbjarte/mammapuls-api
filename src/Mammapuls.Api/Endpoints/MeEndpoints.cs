using System.Security.Claims;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mammapuls.Api.Endpoints;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetAsync)
            .WithName("GetMe")
            .WithSummary("Get the signed-in user's profile")
            .WithTags("Me");

        group.MapDelete("/me", DeleteAsync)
            .WithName("DeleteMe")
            .WithSummary("Delete the signed-in user's account and data (GDPR) and sign out")
            .WithTags("Me");

        return group;
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IUserStore users,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var user = await users.GetAsync(principal.GetUserId(), cancellationToken);
        if (user is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new MeResponse(user.Id, user.Name, user.Email, user.PhoneNumber, user.CreatedAt));
    }

    private static async Task<NoContent> DeleteAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IUserStore users,
        CancellationToken cancellationToken)
    {
        await users.DeleteAsync(principal.GetUserId(), cancellationToken);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }
}

public sealed record MeResponse(string Id, string? Name, string? Email, string? PhoneNumber, DateTimeOffset CreatedAt);
