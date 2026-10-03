using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mammapuls.Api.CheckIns;

public static class CheckInStoreExtensions
{
    public const string ContainerConnectionName = "checkins";

    public static IHostApplicationBuilder AddCheckInStore(this IHostApplicationBuilder builder)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        builder.AddKeyedAzureCosmosContainer(
            ContainerConnectionName,
            configureClientOptions: options => options.UseSystemTextJsonSerializerWithOptions = serializerOptions);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ICheckInStore, CosmosCheckInStore>();
        return builder;
    }
}
