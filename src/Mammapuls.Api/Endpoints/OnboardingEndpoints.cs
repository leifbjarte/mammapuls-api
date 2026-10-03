using System.Security.Claims;
using System.Text.Json;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Onboarding;
using Mammapuls.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Mammapuls.Api.Endpoints;

public static class OnboardingEndpoints
{
    private const int MaxRequestBodyBytes = 80 * 1024;
    private static readonly string[] EnumFieldNames =
    [
        "sleepPerNight",
        "stepsPerDay",
        "trainingDaysPerWeek",
        "foodAndExerciseRelationship",
    ];

    public static RouteGroupBuilder MapOnboardingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/me/onboarding", PutAsync)
            .WithName("PutMyOnboarding")
            .WithSummary("Create or replace the signed-in user's onboarding questionnaire")
            .WithDescription("Stores the user's current questionnaire answers. User identity and timestamps are assigned by the API.")
            .WithTags("Onboarding")
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .Accepts<OnboardingRequest>("application/json")
            .Produces<OnboardingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/me/onboarding", GetAsync)
            .WithName("GetMyOnboarding")
            .WithSummary("Get the signed-in user's onboarding questionnaire")
            .WithDescription("Returns the current questionnaire submission, if one exists.")
            .WithTags("Onboarding")
            .Produces<OnboardingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<OnboardingResponse>, ValidationProblem, UnauthorizedHttpResult, ProblemHttpResult>> PutAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IUserStore users,
        IOnboardingStore onboarding,
        TimeProvider timeProvider,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType())
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (context.Request.ContentLength > MaxRequestBodyBytes)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        OnboardingRequest? request;
        HashSet<string> combinedEnumFields;
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
            combinedEnumFields = FindCombinedEnumFields(document.RootElement);
            request = document.RootElement.Deserialize<OnboardingRequest>(jsonOptions.Value.SerializerOptions);
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = principal.GetUserId();

        if (await users.GetAsync(userId, cancellationToken) is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.Unauthorized();
        }

        var normalized = Normalize(request);
        var errors = Validate(normalized, combinedEnumFields);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var existing = await onboarding.GetAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var submission = ToSubmission(normalized, userId, existing?.CreatedAt ?? now, now);
        var saved = await onboarding.UpsertAsync(submission, cancellationToken);
        return TypedResults.Ok(OnboardingResponse.From(saved));
    }

    private static async Task<Results<Ok<OnboardingResponse>, NotFound>> GetAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IOnboardingStore onboarding,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var submission = await onboarding.GetAsync(principal.GetUserId(), cancellationToken);
        return submission is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(OnboardingResponse.From(submission));
    }

    private static OnboardingRequest Normalize(OnboardingRequest request) => request with
    {
        FullName = request.FullName?.Trim(),
        AllergyDetails = NormalizeOptional(request.AllergyDetails),
        InjuryDetails = NormalizeOptional(request.InjuryDetails),
        PreviousWeightLossExperience = NormalizeOptional(request.PreviousWeightLossExperience),
        GoalAfterEightWeeks = request.GoalAfterEightWeeks?.Trim(),
        BiggestChallenge = request.BiggestChallenge?.Trim(),
        AnythingElseForCoach = request.AnythingElseForCoach?.Trim(),
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string[]> Validate(OnboardingRequest request, HashSet<string> combinedEnumFields)
    {
        var errors = new Dictionary<string, string[]>();

        void AddError(string field, string message) =>
            errors[JsonNamingPolicy.CamelCase.ConvertName(field)] = [message];

        void RequiredText(string field, string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(field, "This field is required and must not be blank.");
            }
            else if (value.Length > maxLength)
            {
                AddError(field, $"Must be at most {maxLength} characters.");
            }
        }

        void RequiredValue<T>(string field, T? value) where T : struct
        {
            if (value is null)
            {
                AddError(field, "This field is required.");
            }
        }

        void RequiredEnum<T>(string field, T? value) where T : struct, Enum
        {
            if (value is null)
            {
                AddError(field, "This field is required.");
            }
            else if (!Enum.IsDefined(value.Value) || combinedEnumFields.Contains(JsonNamingPolicy.CamelCase.ConvertName(field)))
            {
                AddError(field, "Must be a defined value.");
            }
        }

        void Range(string field, int? value, int minimum, int maximum)
        {
            RequiredValue(field, value);
            if (value is not null && (value < minimum || value > maximum))
            {
                AddError(field, $"Must be between {minimum} and {maximum}.");
            }
        }

        void RangeDecimal(string field, decimal? value, decimal minimum, decimal maximum)
        {
            RequiredValue(field, value);
            if (value is not null && (value < minimum || value > maximum))
            {
                AddError(field, $"Must be between {minimum} and {maximum}.");
            }
        }

        RequiredText(nameof(request.FullName), request.FullName, 200);
        Range(nameof(request.Age), request.Age, 15, 120);
        RequiredEnum(nameof(request.SleepPerNight), request.SleepPerNight);
        Range(nameof(request.StressLevel), request.StressLevel, 1, 10);
        RangeDecimal(nameof(request.WeightKg), request.WeightKg, 20, 500);
        RangeDecimal(nameof(request.HeightCm), request.HeightCm, 100, 250);
        RangeDecimal(nameof(request.WaistCm), request.WaistCm, 20, 300);
        RangeDecimal(nameof(request.ChestCm), request.ChestCm, 20, 300);
        RangeDecimal(nameof(request.HipCm), request.HipCm, 20, 300);
        RequiredValue(nameof(request.CanJogComfortably), request.CanJogComfortably);
        RequiredEnum(nameof(request.StepsPerDay), request.StepsPerDay);
        RequiredEnum(nameof(request.TrainingDaysPerWeek), request.TrainingDaysPerWeek);
        RequiredValue(nameof(request.IsVegetarian), request.IsVegetarian);
        RequiredValue(nameof(request.IsPescetarian), request.IsPescetarian);
        RequiredValue(nameof(request.IsBreastfeeding), request.IsBreastfeeding);
        RequiredValue(nameof(request.HasAllergies), request.HasAllergies);
        OptionalDetails(nameof(request.AllergyDetails), request.AllergyDetails, request.HasAllergies);
        RequiredValue(nameof(request.HasInjuries), request.HasInjuries);
        OptionalDetails(nameof(request.InjuryDetails), request.InjuryDetails, request.HasInjuries);
        RequiredValue(nameof(request.HasTriedWeightLossBefore), request.HasTriedWeightLossBefore);
        OptionalDetails(nameof(request.PreviousWeightLossExperience), request.PreviousWeightLossExperience, request.HasTriedWeightLossBefore);
        RequiredEnum(nameof(request.FoodAndExerciseRelationship), request.FoodAndExerciseRelationship);
        RequiredValue(nameof(request.HasHadEatingDisorder), request.HasHadEatingDisorder);
        Range(nameof(request.HealthSatisfaction), request.HealthSatisfaction, 1, 10);
        Range(nameof(request.ProgramMotivation), request.ProgramMotivation, 1, 10);
        RequiredText(nameof(request.GoalAfterEightWeeks), request.GoalAfterEightWeeks, 2000);
        RequiredText(nameof(request.BiggestChallenge), request.BiggestChallenge, 2000);
        RequiredText(nameof(request.AnythingElseForCoach), request.AnythingElseForCoach, 2000);

        return errors;

        void OptionalDetails(string field, string? value, bool? answer)
        {
            if (value is { Length: > 2000 })
            {
                AddError(field, "Must be at most 2000 characters.");
            }
            else if (answer == false && value is not null)
            {
                AddError(field, "Must be empty when the corresponding answer is false.");
            }
        }
    }

    private static HashSet<string> FindCombinedEnumFields(JsonElement payload)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var enumField = Array.Find(EnumFieldNames, field => string.Equals(field, property.Name, StringComparison.OrdinalIgnoreCase));
            if (enumField is not null
                && property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString()?.Contains(',') == true)
            {
                fields.Add(enumField);
            }
        }

        return fields;
    }

    private static OnboardingSubmission ToSubmission(
        OnboardingRequest request,
        string userId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => new(
        userId,
        userId,
        request.FullName!,
        request.Age!.Value,
        request.SleepPerNight!.Value,
        request.StressLevel!.Value,
        request.WeightKg!.Value,
        request.HeightCm!.Value,
        request.WaistCm!.Value,
        request.ChestCm!.Value,
        request.HipCm!.Value,
        request.CanJogComfortably!.Value,
        request.StepsPerDay!.Value,
        request.TrainingDaysPerWeek!.Value,
        request.IsVegetarian!.Value,
        request.IsPescetarian!.Value,
        request.IsBreastfeeding!.Value,
        request.HasAllergies!.Value,
        request.AllergyDetails,
        request.HasInjuries!.Value,
        request.InjuryDetails,
        request.HasTriedWeightLossBefore!.Value,
        request.PreviousWeightLossExperience,
        request.FoodAndExerciseRelationship!.Value,
        request.HasHadEatingDisorder!.Value,
        request.HealthSatisfaction!.Value,
        request.ProgramMotivation!.Value,
        request.GoalAfterEightWeeks!,
        request.BiggestChallenge!,
        request.AnythingElseForCoach!,
        createdAt,
        updatedAt);
}