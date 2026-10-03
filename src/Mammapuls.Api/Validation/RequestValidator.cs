using System.Text.Json;

namespace Mammapuls.Api.Validation;

/// <summary>Field-level validation shared by questionnaire-style endpoints (onboarding, weekly check-in).</summary>
public sealed class RequestValidator(HashSet<string> combinedEnumFields)
{
    private readonly Dictionary<string, string[]> errors = [];

    public Dictionary<string, string[]> Errors => errors;

    public void AddError(string field, string message) =>
        errors[JsonNamingPolicy.CamelCase.ConvertName(field)] = [message];

    public void RequiredText(string field, string? value, int maxLength)
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

    public void OptionalText(string field, string? value, int maxLength)
    {
        if (value is { Length: > 0 } && value.Length > maxLength)
        {
            AddError(field, $"Must be at most {maxLength} characters.");
        }
    }

    public void RequiredValue<T>(string field, T? value) where T : struct
    {
        if (value is null)
        {
            AddError(field, "This field is required.");
        }
    }

    public void RequiredEnum<T>(string field, T? value) where T : struct, Enum
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

    public void Range(string field, int? value, int minimum, int maximum)
    {
        RequiredValue(field, value);
        if (value is not null && (value < minimum || value > maximum))
        {
            AddError(field, $"Must be between {minimum} and {maximum}.");
        }
    }

    public void RangeDecimal(string field, decimal? value, decimal minimum, decimal maximum)
    {
        RequiredValue(field, value);
        if (value is not null && (value < minimum || value > maximum))
        {
            AddError(field, $"Must be between {minimum} and {maximum}.");
        }
    }

    /// <summary>Validates the body measurements both questionnaires collect.</summary>
    public void BodyMeasurements(IBodyMeasurements request)
    {
        RangeDecimal(nameof(request.WeightKg), request.WeightKg, 20, 500);
        RangeDecimal(nameof(request.WaistCm), request.WaistCm, 20, 300);
        RangeDecimal(nameof(request.ChestCm), request.ChestCm, 20, 300);
        RangeDecimal(nameof(request.HipCm), request.HipCm, 20, 300);
    }

    public void StressLevel(int? value) => Range("StressLevel", value, 1, 10);

    public void OptionalDetails(string field, string? value, bool? answer)
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

public interface IBodyMeasurements
{
    decimal? WeightKg { get; }
    decimal? WaistCm { get; }
    decimal? ChestCm { get; }
    decimal? HipCm { get; }
}

public static class RequestText
{
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
