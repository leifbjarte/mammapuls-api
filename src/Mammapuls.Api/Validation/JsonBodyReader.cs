using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mammapuls.Api.Validation;

public sealed record JsonBody<T>(T? Value, HashSet<string> CombinedEnumFields, ProblemHttpResult? Problem) where T : class;

/// <summary>Reads size-limited JSON bodies and records enum fields sent as comma-combined names so they can be rejected.</summary>
public static class JsonBodyReader
{
    public static async Task<JsonBody<T>> ReadAsync<T>(
        HttpContext context,
        JsonSerializerOptions serializerOptions,
        int maxBytes,
        CancellationToken cancellationToken) where T : class
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType())
        {
            return Fail<T>(StatusCodes.Status415UnsupportedMediaType);
        }

        if (context.Request.ContentLength > maxBytes)
        {
            return Fail<T>(StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
            var combined = FindCombinedEnumFields<T>(document.RootElement);
            var value = document.RootElement.Deserialize<T>(serializerOptions);
            return value is null ? Fail<T>(StatusCodes.Status400BadRequest) : new JsonBody<T>(value, combined, null);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Fail<T>(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Fail<T>(StatusCodes.Status400BadRequest);
        }
    }

    private static JsonBody<T> Fail<T>(int status) where T : class =>
        new(null, [], TypedResults.Problem(statusCode: status));

    private static HashSet<string> FindCombinedEnumFields<T>(JsonElement payload)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        var enumFields = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType).IsEnum)
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .ToArray();

        foreach (var property in payload.EnumerateObject())
        {
            var enumField = Array.Find(enumFields, field => string.Equals(field, property.Name, StringComparison.OrdinalIgnoreCase));
            if (enumField is not null
                && property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString()?.Contains(',') == true)
            {
                fields.Add(enumField);
            }
        }

        return fields;
    }
}
