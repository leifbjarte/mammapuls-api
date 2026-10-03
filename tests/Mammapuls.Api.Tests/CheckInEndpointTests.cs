using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mammapuls.Api.Auth;
using Mammapuls.Api.CheckIns;

namespace Mammapuls.Api.Tests;

public sealed class CheckInEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Base = "/api/v1/me/check-ins";
    private const string Week = "2026-W01";
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anonymous_Returns401()
    {
        using var client = factory.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, Base, null, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", "{}", null, csrf: true)).StatusCode);
    }

    [Fact]
    public async Task Put_WithoutCsrfHeader_Returns400()
    {
        var user = factory.Users.Seed("No csrf");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.CheckIns.GetAsync(user.Id, Week, Ct));
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsAndNormalizesText()
    {
        var user = factory.Users.Seed("Check-in user");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["whatWorkedWell"] = "  Meal prep  ";
        payload["proteinChallenge"] = "  ";

        var put = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("no-store", put.Headers.CacheControl?.ToString());
        var created = await ReadAsync<CheckInResponse>(put);
        Assert.Equal(Week, created.Week);
        Assert.Equal("Meal prep", created.WhatWorkedWell);
        Assert.Null(created.ProteinChallenge);
        Assert.Equal(72.5m, created.WeightKg);
        Assert.Equal(StrengthSessions.MoreThanThree, created.StrengthSessions);

        var get = await SendAsync(client, HttpMethod.Get, $"{Base}/{Week}", null, factory.CreateSessionCookie(user.Id));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(created, await ReadAsync<CheckInResponse>(get));
        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync(Ct));
        Assert.Equal("moreThanThree", json.RootElement.GetProperty("strengthSessions").GetString());
    }

    [Fact]
    public async Task Get_Missing_Returns404()
    {
        var user = factory.Users.Seed("Empty");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Get, $"{Base}/{Week}", null, factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_Replace_PreservesCreatedAtAndKeepsOneDocumentPerWeek()
    {
        var user = factory.Users.Seed("Replace");
        using var client = factory.CreateApiClient();
        var cookie = factory.CreateSessionCookie(user.Id);
        var first = await ReadAsync<CheckInResponse>(await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), cookie, csrf: true));
        await Task.Delay(25, Ct);
        var payload = ValidRequest();
        payload["weightKg"] = 71;

        var second = await ReadAsync<CheckInResponse>(await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", payload.ToJsonString(), cookie, csrf: true));

        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.True(second.UpdatedAt > first.UpdatedAt);
        Assert.Equal(71m, second.WeightKg);
        Assert.Single(await factory.CheckIns.ListAsync(user.Id, Ct));
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnCheckInsNewestFirst()
    {
        var userA = factory.Users.Seed("List A");
        var userB = factory.Users.Seed("List B");
        using var client = factory.CreateApiClient();
        foreach (var week in new[] { "2026-W01", "2026-W02" })
        {
            await SendAsync(client, HttpMethod.Put, $"{Base}/{week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(userA.Id), csrf: true);
        }

        await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(userB.Id), csrf: true);

        var response = await SendAsync(client, HttpMethod.Get, Base, null, factory.CreateSessionCookie(userA.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await ReadAsync<CheckInResponse[]>(response);
        Assert.Equal(["2026-W02", "2026-W01"], list.Select(c => c.Week));
    }

    [Theory]
    [InlineData("2026-1")]
    [InlineData("2026-W00")]
    [InlineData("2026-W54")]
    [InlineData("2026-W7")]
    [InlineData("abcd-W01")]
    [InlineData("9999-W01")]
    public async Task Put_InvalidOrFutureWeek_Returns400(string week)
    {
        var user = factory.Users.Seed("Bad week");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.CheckIns.ListAsync(user.Id, Ct));
    }

    [Theory]
    [InlineData("weightKg", "19.9", 400)]
    [InlineData("weightKg", "20", 200)]
    [InlineData("waistCm", "300.1", 400)]
    [InlineData("proteinTargetDays", "8", 400)]
    [InlineData("proteinTargetDays", "0", 200)]
    [InlineData("calorieTargetDays", "-1", 400)]
    [InlineData("stressLevel", "0", 400)]
    [InlineData("stressLevel", "10", 200)]
    [InlineData("sleepQuality", "11", 400)]
    [InlineData("averageStepsPerDay", "100001", 400)]
    [InlineData("averageKcalPerDay", "-5", 400)]
    [InlineData("strengthSessions", "\"four\"", 400)]
    [InlineData("strengthSessions", "1", 400)]
    [InlineData("intervalSessions", "\"zero,one\"", 400)]
    [InlineData("intervalSessions", "\"moreThanTwo\"", 200)]
    public async Task Put_FieldBoundaries(string field, string jsonValue, int expectedStatus)
    {
        var user = factory.Users.Seed("Boundary");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = JsonNode.Parse(jsonValue);

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.Equal(expectedStatus == 200, await factory.CheckIns.GetAsync(user.Id, Week, Ct) is not null);
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("weightKg")]
    [InlineData("strengthSessions")]
    [InlineData("whatWorkedWell")]
    [InlineData("whatWasChallenging")]
    [InlineData("needHelpWith")]
    public async Task Put_RequiredFieldMissingOrBlank_Returns400(string field)
    {
        var user = factory.Users.Seed("Required");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = null;

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.CheckIns.ListAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_OptionalChallengesMayBeOmittedAndTextIsLimited()
    {
        var user = factory.Users.Seed("Optional");
        using var client = factory.CreateApiClient();
        var omitted = ValidRequest();
        omitted.Remove("proteinChallenge");
        omitted.Remove("calorieChallenge");
        var tooLong = ValidRequest();
        tooLong["calorieChallenge"] = new string('x', 2001);

        var ok = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", omitted.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);
        var bad = await SendAsync(client, HttpMethod.Put, $"{Base}/2026-W02", tooLong.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Put_UnsupportedContentType_Returns415_AndOversizedBody_Returns413()
    {
        var user = factory.Users.Seed("Transport");
        using var client = factory.CreateApiClient();
        var cookie = factory.CreateSessionCookie(user.Id);

        var wrongType = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), cookie, csrf: true, contentType: "text/plain");
        var tooBig = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString() + new string(' ', 40 * 1024), cookie, csrf: true);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
    }

    [Fact]
    public async Task Put_StaleUserCookie_Returns401AndStoresNothing()
    {
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie("deleted-check-in-user"), csrf: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await factory.CheckIns.ListAsync("deleted-check-in-user", Ct));
    }

    [Fact]
    public async Task DeleteMe_CascadesOnlyTheCurrentUsersCheckIns()
    {
        var userA = factory.Users.Seed("Delete A");
        var userB = factory.Users.Seed("Keep B");
        using var client = factory.CreateApiClient();
        await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(userA.Id), csrf: true);
        await SendAsync(client, HttpMethod.Put, $"{Base}/{Week}", ValidRequest().ToJsonString(), factory.CreateSessionCookie(userB.Id), csrf: true);

        var response = await SendAsync(client, HttpMethod.Delete, "/api/v1/me", null, factory.CreateSessionCookie(userA.Id), csrf: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await factory.CheckIns.ListAsync(userA.Id, Ct));
        Assert.Single(await factory.CheckIns.ListAsync(userB.Id, Ct));
    }

    private static JsonObject ValidRequest() => new()
    {
        ["fullName"] = "Kari Nordmann",
        ["weightKg"] = 72.5,
        ["waistCm"] = 82.5,
        ["hipCm"] = 101.5,
        ["chestCm"] = 94.5,
        ["averageStepsPerDay"] = 8500,
        ["averageKcalPerDay"] = 1800,
        ["proteinTargetDays"] = 5,
        ["proteinChallenge"] = "Travel",
        ["calorieTargetDays"] = 6,
        ["calorieChallenge"] = "Weekend dinner",
        ["strengthSessions"] = "moreThanThree",
        ["intervalSessions"] = "one",
        ["stressLevel"] = 6,
        ["sleepQuality"] = 7,
        ["whatWorkedWell"] = "Meal prep",
        ["whatWasChallenging"] = "Evenings",
        ["needHelpWith"] = "Nothing",
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(Ct), ResponseJsonOptions)!;

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? body,
        string? cookie,
        bool csrf = false,
        string contentType = "application/json")
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (csrf)
        {
            request.Headers.Add(AuthConstants.CsrfHeaderName, "XMLHttpRequest");
        }

        return await client.SendAsync(request, Ct);
    }

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
