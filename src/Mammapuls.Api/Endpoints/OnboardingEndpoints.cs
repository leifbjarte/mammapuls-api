using System.Security.Claims;
using System.Text.Json;
using Mammapuls.Api.Auth;
using Mammapuls.Api.Onboarding;
using Mammapuls.Api.Users;
using Mammapuls.Api.Validation;
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
        var body = await JsonBodyReader.ReadAsync<OnboardingRequest>(context, jsonOptions.Value.SerializerOptions, MaxRequestBodyBytes, cancellationToken);
        if (body.Problem is not null)
        {
            return body.Problem;
        }

        var request = body.Value!;
        var combinedEnumFields = body.CombinedEnumFields;
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
        AllergyDetails = RequestText.NormalizeOptional(request.AllergyDetails),
        InjuryDetails = RequestText.NormalizeOptional(request.InjuryDetails),
        PreviousWeightLossExperience = RequestText.NormalizeOptional(request.PreviousWeightLossExperience),
        GoalAfterEightWeeks = request.GoalAfterEightWeeks?.Trim(),
        BiggestChallenge = request.BiggestChallenge?.Trim(),
        AnythingElseForCoach = request.AnythingElseForCoach?.Trim(),
    };

    private static Dictionary<string, string[]> Validate(OnboardingRequest request, HashSet<string> combinedEnumFields)
    {
        var v = new RequestValidator(combinedEnumFields);

        v.RequiredText(nameof(request.FullName), request.FullName, 200);
        v.Range(nameof(request.Age), request.Age, 15, 120);
        v.RequiredEnum(nameof(request.SleepPerNight), request.SleepPerNight);
        v.StressLevel(request.StressLevel);
        v.BodyMeasurements(request);
        v.RangeDecimal(nameof(request.HeightCm), request.HeightCm, 100, 250);
        v.RequiredValue(nameof(request.CanJogComfortably), request.CanJogComfortably);
        v.RequiredEnum(nameof(request.StepsPerDay), request.StepsPerDay);
        v.RequiredEnum(nameof(request.TrainingDaysPerWeek), request.TrainingDaysPerWeek);
        v.RequiredValue(nameof(request.IsVegetarian), request.IsVegetarian);
        v.RequiredValue(nameof(request.IsPescetarian), request.IsPescetarian);
        v.RequiredValue(nameof(request.IsBreastfeeding), request.IsBreastfeeding);
        v.RequiredValue(nameof(request.HasAllergies), request.HasAllergies);
        v.OptionalDetails(nameof(request.AllergyDetails), request.AllergyDetails, request.HasAllergies);
        v.RequiredValue(nameof(request.HasInjuries), request.HasInjuries);
        v.OptionalDetails(nameof(request.InjuryDetails), request.InjuryDetails, request.HasInjuries);
        v.RequiredValue(nameof(request.HasTriedWeightLossBefore), request.HasTriedWeightLossBefore);
        v.OptionalDetails(nameof(request.PreviousWeightLossExperience), request.PreviousWeightLossExperience, request.HasTriedWeightLossBefore);
        v.RequiredEnum(nameof(request.FoodAndExerciseRelationship), request.FoodAndExerciseRelationship);
        v.RequiredValue(nameof(request.HasHadEatingDisorder), request.HasHadEatingDisorder);
        v.Range(nameof(request.HealthSatisfaction), request.HealthSatisfaction, 1, 10);
        v.Range(nameof(request.ProgramMotivation), request.ProgramMotivation, 1, 10);
        v.RequiredText(nameof(request.GoalAfterEightWeeks), request.GoalAfterEightWeeks, 2000);
        v.RequiredText(nameof(request.BiggestChallenge), request.BiggestChallenge, 2000);
        v.RequiredText(nameof(request.AnythingElseForCoach), request.AnythingElseForCoach, 2000);

        return v.Errors;
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