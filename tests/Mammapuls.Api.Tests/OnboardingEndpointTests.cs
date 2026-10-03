using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Onboarding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mammapuls.Api.Tests;

public sealed class OnboardingEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Path = "/api/v1/me/onboarding";
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401NotRedirect()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(Path, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Put_Anonymous_Returns401NotRedirect()
    {
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, "{}", cookie: null, csrf: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Put_WithoutCsrfHeader_Returns400AndDoesNotPersist()
    {
        var user = factory.Users.Seed("Onboarding user");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, "{}", factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_UnsupportedContentType_Returns415AndDoesNotPersist()
    {
        var user = factory.Users.Seed("Unsupported content type");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(
            client,
            HttpMethod.Put,
            ValidRequest().ToJsonString(),
            factory.CreateSessionCookie(user.Id),
            csrf: true,
            contentType: "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Get_BeforePut_Returns404AndNoStore()
    {
        var user = factory.Users.Seed("New onboarding user");
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Get, body: null, factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsEveryAnswerAsCamelCaseEnumStrings()
    {
        var user = factory.Users.Seed("Questionnaire user");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["fullName"] = "  Kari Nordmann  ";
        payload["goalAfterEightWeeks"] = "  Feel stronger  ";
        payload["biggestChallenge"] = "  Finding time  ";
        payload["anythingElseForCoach"] = "  Coach note  ";

        var put = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("no-store", put.Headers.CacheControl?.ToString());
        var created = await ReadResponseAsync(put);
        var expected = new OnboardingResponse(
            "Kari Nordmann", 34, SleepPerNight.SixToSevenHours, 6, 72.5m, 168.5m, 82.5m, 94.5m, 101.5m,
            true, StepsPerDay.From5000To10000, TrainingDaysPerWeek.OneToTwo, false, true, false, true, "Peanuts",
            true, "Old knee injury", true, "Tried before", FoodAndExerciseRelationship.Difficult, false, 7, 8,
            "Feel stronger", "Finding time", "Coach note", default, default);
        Assert.Equal(expected, created with { CreatedAt = default, UpdatedAt = default });
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        var get = await SendAsync(client, HttpMethod.Get, body: null, factory.CreateSessionCookie(user.Id));

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("no-store", get.Headers.CacheControl?.ToString());
        Assert.Equal(created, await ReadResponseAsync(get));
        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync(Ct));
        Assert.Equal("sixToSevenHours", json.RootElement.GetProperty("sleepPerNight").GetString());
        Assert.Equal("from5000To10000", json.RootElement.GetProperty("stepsPerDay").GetString());
        Assert.Equal("oneToTwo", json.RootElement.GetProperty("trainingDaysPerWeek").GetString());
        Assert.Equal("difficult", json.RootElement.GetProperty("foodAndExerciseRelationship").GetString());
    }

    [Fact]
    public async Task Put_ReplacePreservesCreatedAtAndRefreshesUpdatedAt()
    {
        var user = factory.Users.Seed("Replace onboarding");
        using var client = factory.CreateApiClient();
        var cookie = factory.CreateSessionCookie(user.Id);

        var first = await SendAsync(client, HttpMethod.Put, ValidRequest().ToJsonString(), cookie, csrf: true);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstSubmission = await ReadResponseAsync(first);
        await Task.Delay(25, Ct);

        var replacement = ValidRequest();
        replacement["fullName"] = "Replacement name";
        replacement["age"] = 35;
        var second = await SendAsync(client, HttpMethod.Put, replacement.ToJsonString(), cookie, csrf: true);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var replacedSubmission = await ReadResponseAsync(second);
        Assert.Equal(firstSubmission.CreatedAt, replacedSubmission.CreatedAt);
        Assert.True(replacedSubmission.UpdatedAt > firstSubmission.UpdatedAt);
        Assert.Equal("Replacement name", replacedSubmission.FullName);
        Assert.Equal(35, replacedSubmission.Age);
    }

    [Fact]
    public async Task Put_TwoUsers_AreIsolatedAndBodyUserIdIsIgnored()
    {
        var userA = factory.Users.Seed("Questionnaire A");
        var userB = factory.Users.Seed("Questionnaire B");
        using var client = factory.CreateApiClient();
        var payloadA = ValidRequest();
        payloadA["fullName"] = "User A answers";
        payloadA["userId"] = userB.Id;

        var putA = await SendAsync(client, HttpMethod.Put, payloadA.ToJsonString(), factory.CreateSessionCookie(userA.Id), csrf: true);
        var putB = await SendAsync(client, HttpMethod.Put, ValidRequest().ToJsonString(), factory.CreateSessionCookie(userB.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, putB.StatusCode);
        var getA = await SendAsync(client, HttpMethod.Get, body: null, factory.CreateSessionCookie(userA.Id));
        var getB = await SendAsync(client, HttpMethod.Get, body: null, factory.CreateSessionCookie(userB.Id));
        Assert.Equal("User A answers", (await ReadResponseAsync(getA)).FullName);
        Assert.Equal("Kari Nordmann", (await ReadResponseAsync(getB)).FullName);

        var storedA = await factory.Onboarding.GetAsync(userA.Id, Ct);
        var storedB = await factory.Onboarding.GetAsync(userB.Id, Ct);
        Assert.NotNull(storedA);
        Assert.NotNull(storedB);
        Assert.Equal(userA.Id, storedA.UserId);
        Assert.Equal(userA.Id, storedA.Id);
        Assert.Equal(userB.Id, storedB.UserId);
        Assert.Equal(userB.Id, storedB.Id);
    }

    [Theory]
    [MemberData(nameof(RangeCases))]
    public async Task Put_RangeFields_EnforcesInclusiveBoundaries(string field, string jsonValue, int expectedStatus)
    {
        var user = factory.Users.Seed("Range test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = JsonNode.Parse(jsonValue);

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.Equal(expectedStatus == (int)HttpStatusCode.OK, await factory.Onboarding.GetAsync(user.Id, Ct) is not null);
    }

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public async Task Put_RequiredFieldNull_Returns400WithoutPersistence(string field)
    {
        var user = factory.Users.Seed("Required field test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = null;

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("goalAfterEightWeeks")]
    [InlineData("biggestChallenge")]
    [InlineData("anythingElseForCoach")]
    public async Task Put_RequiredTextBlank_Returns400(string field)
    {
        var user = factory.Users.Seed("Blank field test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = " \t ";

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Theory]
    [MemberData(nameof(TextLengthCases))]
    public async Task Put_TextOverMaximumLength_Returns400(string field, int maximumLength)
    {
        var user = factory.Users.Seed("Text length test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = new string('x', maximumLength + 1);

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_TextAtMaximumLengths_IsAccepted()
    {
        var user = factory.Users.Seed("Maximum text test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["fullName"] = new string('n', 200);
        foreach (var field in new[] { "goalAfterEightWeeks", "biggestChallenge", "anythingElseForCoach", "allergyDetails", "injuryDetails", "previousWeightLossExperience" })
        {
            payload[field] = new string('x', 2000);
        }

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_MaximumLengthEscapedUnicodePayload_IsAccepted()
    {
        var user = factory.Users.Seed("Maximum Unicode onboarding test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["fullName"] = new string('\u00e9', 200);
        foreach (var field in new[] { "goalAfterEightWeeks", "biggestChallenge", "anythingElseForCoach", "allergyDetails", "injuryDetails", "previousWeightLossExperience" })
        {
            payload[field] = new string('\u00e9', 2000);
        }

        var body = payload.ToJsonString();
        Assert.True(Encoding.UTF8.GetByteCount(body) > 32 * 1024);
        var response = await SendAsync(client, HttpMethod.Put, body, factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_RequestBodyOverMaximumSize_Returns413()
    {
        var user = factory.Users.Seed("Oversized onboarding test");
        using var client = factory.CreateApiClient();
        var body = ValidRequest().ToJsonString() + new string(' ', 80 * 1024);

        var response = await SendAsync(client, HttpMethod.Put, body, factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_OptionalDetailsWhenAnswersTrue_AcceptsNullAndNormalizesBlankToNull()
    {
        var user = factory.Users.Seed("Optional details test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["allergyDetails"] = null;
        payload["injuryDetails"] = " \t ";
        payload["previousWeightLossExperience"] = "";

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await ReadResponseAsync(response);
        Assert.Null(saved.AllergyDetails);
        Assert.Null(saved.InjuryDetails);
        Assert.Null(saved.PreviousWeightLossExperience);
    }

    [Theory]
    [MemberData(nameof(ConditionalDetails))]
    public async Task Put_NonBlankDetailsWhenAnswerFalse_Returns400(string answerField, string detailsField)
    {
        var user = factory.Users.Seed("Conditional details test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[answerField] = false;
        payload[detailsField] = "Not allowed when false";

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_StaleUserCookie_Returns401ClearsCookieAndStoresNothing()
    {
        const string staleUserId = "deleted-onboarding-user";
        using var client = factory.CreateApiClient();

        var response = await SendAsync(client, HttpMethod.Put, ValidRequest().ToJsonString(), factory.CreateSessionCookie(staleUserId), csrf: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSessionCookieCleared(response);
        Assert.Null(await factory.Onboarding.GetAsync(staleUserId, Ct));
    }

    [Fact]
    public async Task DeleteMe_CascadesOnlyTheCurrentUsersOnboarding()
    {
        var userA = factory.Users.Seed("Delete onboarding A");
        var userB = factory.Users.Seed("Keep onboarding B");
        using var client = factory.CreateApiClient();
        await SendAsync(client, HttpMethod.Put, ValidRequest().ToJsonString(), factory.CreateSessionCookie(userA.Id), csrf: true);
        await SendAsync(client, HttpMethod.Put, ValidRequest().ToJsonString(), factory.CreateSessionCookie(userB.Id), csrf: true);

        var response = await SendAsync(
            client,
            HttpMethod.Delete,
            body: null,
            factory.CreateSessionCookie(userA.Id),
            csrf: true,
            path: "/api/v1/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(userA.Id, Ct));
        Assert.NotNull(await factory.Onboarding.GetAsync(userB.Id, Ct));
        Assert.False(factory.Users.Contains(userA.Id));
        Assert.True(factory.Users.Contains(userB.Id));
    }

    [Fact]
    public async Task Put_SuccessAndValidationFailure_DoNotLogSubmittedValues()
    {
        using var client = factory.CreateApiClient();
        var provider = new CaptureLoggerProvider();
        factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(provider);
        var user = factory.Users.Seed("Log capture test");
        const string successSentinel = "ONBOARDING_SUCCESS_VALUE_DO_NOT_LOG";
        const string failureSentinel = "ONBOARDING_FAILURE_VALUE_DO_NOT_LOG";
        var success = ValidRequest();
        success["anythingElseForCoach"] = successSentinel;
        var invalid = ValidRequest();
        invalid["fullName"] = failureSentinel;
        invalid["age"] = 14;

        var successfulResponse = await SendAsync(client, HttpMethod.Put, success.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);
        var invalidResponse = await SendAsync(client, HttpMethod.Put, invalid.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.OK, successfulResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.DoesNotContain(failureSentinel, await invalidResponse.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.DoesNotContain(successSentinel, provider.Messages, StringComparison.Ordinal);
        Assert.DoesNotContain(failureSentinel, provider.Messages, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidEnumValues))]
    public async Task Put_EnumIntegerOrUnknownString_Returns400(string field, string jsonValue)
    {
        var user = factory.Users.Seed("Invalid enum test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload[field] = JsonNode.Parse(jsonValue);

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Put_CombinedEnumNames_ReturnsSanitized400()
    {
        var user = factory.Users.Seed("Combined enum test");
        using var client = factory.CreateApiClient();
        var payload = ValidRequest();
        payload["foodAndExerciseRelationship"] = "good,okay";

        var response = await SendAsync(client, HttpMethod.Put, payload.ToJsonString(), factory.CreateSessionCookie(user.Id), csrf: true);
        var responseBody = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("foodAndExerciseRelationship", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("good,okay", responseBody, StringComparison.Ordinal);
        Assert.Null(await factory.Onboarding.GetAsync(user.Id, Ct));
    }

    public static IEnumerable<object[]> RangeCases()
    {
        var ranges = new (string Field, decimal Minimum, decimal Maximum)[]
        {
            ("age", 15, 120),
            ("stressLevel", 1, 10),
            ("weightKg", 20, 500),
            ("heightCm", 100, 250),
            ("waistCm", 20, 300),
            ("chestCm", 20, 300),
            ("hipCm", 20, 300),
            ("healthSatisfaction", 1, 10),
            ("programMotivation", 1, 10),
        };

        foreach (var range in ranges)
        {
            yield return [range.Field, (range.Minimum - 1).ToString(CultureInfo.InvariantCulture), (int)HttpStatusCode.BadRequest];
            yield return [range.Field, range.Minimum.ToString(CultureInfo.InvariantCulture), (int)HttpStatusCode.OK];
            yield return [range.Field, range.Maximum.ToString(CultureInfo.InvariantCulture), (int)HttpStatusCode.OK];
            yield return [range.Field, (range.Maximum + 1).ToString(CultureInfo.InvariantCulture), (int)HttpStatusCode.BadRequest];
        }
    }

    public static IEnumerable<object[]> RequiredFields()
    {
        foreach (var field in new[]
        {
            "fullName", "age", "sleepPerNight", "stressLevel", "weightKg", "heightCm", "waistCm", "chestCm", "hipCm",
            "canJogComfortably", "stepsPerDay", "trainingDaysPerWeek", "isVegetarian", "isPescetarian", "isBreastfeeding",
            "hasAllergies", "hasInjuries", "hasTriedWeightLossBefore", "foodAndExerciseRelationship", "hasHadEatingDisorder",
            "healthSatisfaction", "programMotivation", "goalAfterEightWeeks", "biggestChallenge", "anythingElseForCoach",
        })
        {
            yield return [field];
        }
    }

    public static IEnumerable<object[]> TextLengthCases()
    {
        yield return ["fullName", 200];
        yield return ["goalAfterEightWeeks", 2000];
        yield return ["biggestChallenge", 2000];
        yield return ["anythingElseForCoach", 2000];
        yield return ["allergyDetails", 2000];
        yield return ["injuryDetails", 2000];
        yield return ["previousWeightLossExperience", 2000];
    }

    public static IEnumerable<object[]> ConditionalDetails()
    {
        yield return ["hasAllergies", "allergyDetails"];
        yield return ["hasInjuries", "injuryDetails"];
        yield return ["hasTriedWeightLossBefore", "previousWeightLossExperience"];
    }

    public static IEnumerable<object[]> InvalidEnumValues()
    {
        foreach (var field in new[] { "sleepPerNight", "stepsPerDay", "trainingDaysPerWeek", "foodAndExerciseRelationship" })
        {
            yield return [field, "99"];
            yield return [field, "\"unknown\""];
        }
    }

    private static JsonObject ValidRequest() => JsonNode.Parse("""
        {
          "fullName": "Kari Nordmann",
          "age": 34,
          "sleepPerNight": "sixToSevenHours",
          "stressLevel": 6,
          "weightKg": 72.5,
          "heightCm": 168.5,
          "waistCm": 82.5,
          "chestCm": 94.5,
          "hipCm": 101.5,
          "canJogComfortably": true,
          "stepsPerDay": "from5000To10000",
          "trainingDaysPerWeek": "oneToTwo",
          "isVegetarian": false,
          "isPescetarian": true,
          "isBreastfeeding": false,
          "hasAllergies": true,
          "allergyDetails": "Peanuts",
          "hasInjuries": true,
          "injuryDetails": "Old knee injury",
          "hasTriedWeightLossBefore": true,
          "previousWeightLossExperience": "Tried before",
          "foodAndExerciseRelationship": "difficult",
          "hasHadEatingDisorder": false,
          "healthSatisfaction": 7,
          "programMotivation": 8,
          "goalAfterEightWeeks": "Feel stronger",
          "biggestChallenge": "Finding time",
          "anythingElseForCoach": "Coach note"
        }
        """)!.AsObject();

    private static async Task<OnboardingResponse> ReadResponseAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<OnboardingResponse>(ResponseJsonOptions, Ct)
        ?? throw new InvalidOperationException("Expected an onboarding response body.");

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string? body,
        string? cookie,
        bool csrf = false,
        string path = Path,
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

    private static void AssertSessionCookieCleared(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookies), "Expected a Set-Cookie header.");
        Assert.Contains(setCookies, cookie =>
            cookie.StartsWith($"{AuthConstants.CookieName}=;", StringComparison.Ordinal)
            && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();

        public string Messages => string.Join(Environment.NewLine, messages);

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, messages);

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(string categoryName, ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue($"{categoryName}: {formatter(state, exception)} {exception}");
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}