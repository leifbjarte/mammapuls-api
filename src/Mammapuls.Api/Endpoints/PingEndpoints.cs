namespace Mammapuls.Api.Endpoints;

public static class PingEndpoints
{
    public static RouteGroupBuilder MapPingEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => TypedResults.Ok(new PingResponse("pong", DateTimeOffset.UtcNow)))
            .AllowAnonymous()
            .WithName("Ping")
            .WithSummary("Check that the API is reachable")
            .WithTags("Ping");

        return group;
    }
}

public sealed record PingResponse(string Message, DateTimeOffset ServerTime);
