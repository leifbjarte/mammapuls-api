using System.Globalization;
using System.Security.Claims;
using Mammapuls.Api.Auth;
using Mammapuls.Api.CheckIns;
using Mammapuls.Api.Users;
using Mammapuls.Api.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Mammapuls.Api.Endpoints;

public static class CheckInEndpoints
{
    private const int MaxRequestBodyBytes = 40 * 1024;
    private const int MaxTextLength = 2000;

    public static RouteGroupBuilder MapCheckInEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/me/check-ins/{week}", PutAsync)
            .WithName("PutMyCheckIn")
            .WithSummary("Create or replace the signed-in user's weekly check-in")
            .WithDescription("Stores the check-in for an ISO week (for example 2026-W40). Weeks starting in the future are rejected. User identity and timestamps are assigned by the API.")
            .WithTags("Check-ins")
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .Accepts<CheckInRequest>("application/json")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/me/check-ins/{week}", GetAsync)
            .WithName("GetMyCheckIn")
            .WithSummary("Get the signed-in user's check-in for an ISO week")
            .WithTags("Check-ins")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me/check-ins", ListAsync)
            .WithName("ListMyCheckIns")
            .WithSummary("List the signed-in user's check-ins, newest week first")
            .WithTags("Check-ins")
            .Produces<CheckInResponse[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<CheckInResponse>, ValidationProblem, UnauthorizedHttpResult, ProblemHttpResult>> PutAsync(
        string week,
        ClaimsPrincipal principal,
        HttpContext context,
        IUserStore users,
        ICheckInStore checkIns,
        TimeProvider timeProvider,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var body = await JsonBodyReader.ReadAsync<CheckInRequest>(context, jsonOptions.Value.SerializerOptions, MaxRequestBodyBytes, cancellationToken);
        if (body.Problem is not null)
        {
            return body.Problem;
        }

        var now = timeProvider.GetUtcNow();
        if (!TryNormalizeWeek(week, now, out var normalizedWeek))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["week"] = ["Must be an ISO week like 2026-W40 that has not started yet."],
            });
        }

        var userId = principal.GetUserId();
        if (await users.GetAsync(userId, cancellationToken) is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.Unauthorized();
        }

        var request = Normalize(body.Value!);
        var errors = Validate(request, body.CombinedEnumFields);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var existing = await checkIns.GetAsync(userId, normalizedWeek, cancellationToken);
        var saved = await checkIns.UpsertAsync(ToSubmission(request, userId, normalizedWeek, existing?.CreatedAt ?? now, now), cancellationToken);
        return TypedResults.Ok(CheckInResponse.From(saved));
    }

    private static async Task<Results<Ok<CheckInResponse>, NotFound, ValidationProblem>> GetAsync(
        string week,
        ClaimsPrincipal principal,
        HttpContext context,
        ICheckInStore checkIns,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryNormalizeWeek(week, timeProvider.GetUtcNow(), out var normalizedWeek))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["week"] = ["Must be an ISO week like 2026-W40."],
            });
        }

        var submission = await checkIns.GetAsync(principal.GetUserId(), normalizedWeek, cancellationToken);
        return submission is null ? TypedResults.NotFound() : TypedResults.Ok(CheckInResponse.From(submission));
    }

    private static async Task<Ok<CheckInResponse[]>> ListAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        ICheckInStore checkIns,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var submissions = await checkIns.ListAsync(principal.GetUserId(), cancellationToken);
        return TypedResults.Ok(submissions.Select(CheckInResponse.From).ToArray());
    }

    // Accepts "YYYY-Www" and returns the canonical form; future weeks are rejected (one day of clock slack).
    private static bool TryNormalizeWeek(string value, DateTimeOffset now, out string week)
    {
        week = string.Empty;
        if (value.Length != 8 || value[4] != '-' || value[5] != 'W'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || year < 2000 || number < 1 || number > ISOWeek.GetWeeksInYear(year))
        {
            return false;
        }

        var start = ISOWeek.ToDateTime(year, number, DayOfWeek.Monday);
        if (start > now.UtcDateTime.Date.AddDays(1))
        {
            return false;
        }

        week = $"{year:D4}-W{number:D2}";
        return true;
    }

    private static CheckInRequest Normalize(CheckInRequest request) => request with
    {
        FullName = request.FullName?.Trim(),
        ProteinChallenge = RequestText.NormalizeOptional(request.ProteinChallenge),
        CalorieChallenge = RequestText.NormalizeOptional(request.CalorieChallenge),
        WhatWorkedWell = request.WhatWorkedWell?.Trim(),
        WhatWasChallenging = request.WhatWasChallenging?.Trim(),
        NeedHelpWith = request.NeedHelpWith?.Trim(),
    };

    private static Dictionary<string, string[]> Validate(CheckInRequest request, HashSet<string> combinedEnumFields)
    {
        var v = new RequestValidator(combinedEnumFields);
        v.RequiredText(nameof(request.FullName), request.FullName, 200);
        v.BodyMeasurements(request);
        v.Range(nameof(request.AverageStepsPerDay), request.AverageStepsPerDay, 0, 100_000);
        v.Range(nameof(request.AverageKcalPerDay), request.AverageKcalPerDay, 0, 20_000);
        v.Range(nameof(request.ProteinTargetDays), request.ProteinTargetDays, 0, 7);
        v.OptionalText(nameof(request.ProteinChallenge), request.ProteinChallenge, MaxTextLength);
        v.Range(nameof(request.CalorieTargetDays), request.CalorieTargetDays, 0, 7);
        v.OptionalText(nameof(request.CalorieChallenge), request.CalorieChallenge, MaxTextLength);
        v.RequiredEnum(nameof(request.StrengthSessions), request.StrengthSessions);
        v.RequiredEnum(nameof(request.IntervalSessions), request.IntervalSessions);
        v.StressLevel(request.StressLevel);
        v.Range(nameof(request.SleepQuality), request.SleepQuality, 1, 10);
        v.RequiredText(nameof(request.WhatWorkedWell), request.WhatWorkedWell, MaxTextLength);
        v.RequiredText(nameof(request.WhatWasChallenging), request.WhatWasChallenging, MaxTextLength);
        v.RequiredText(nameof(request.NeedHelpWith), request.NeedHelpWith, MaxTextLength);
        return v.Errors;
    }

    private static CheckInSubmission ToSubmission(
        CheckInRequest request,
        string userId,
        string week,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => new(
        week,
        userId,
        week,
        request.FullName!,
        request.WeightKg!.Value,
        request.WaistCm!.Value,
        request.HipCm!.Value,
        request.ChestCm!.Value,
        request.AverageStepsPerDay!.Value,
        request.AverageKcalPerDay!.Value,
        request.ProteinTargetDays!.Value,
        request.ProteinChallenge,
        request.CalorieTargetDays!.Value,
        request.CalorieChallenge,
        request.StrengthSessions!.Value,
        request.IntervalSessions!.Value,
        request.StressLevel!.Value,
        request.SleepQuality!.Value,
        request.WhatWorkedWell!,
        request.WhatWasChallenging!,
        request.NeedHelpWith!,
        createdAt,
        updatedAt);
}
