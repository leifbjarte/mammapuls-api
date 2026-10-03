using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mammapuls.Api.Onboarding;

public static class OnboardingStoreExtensions
{
    public const string ContainerConnectionName = "onboarding";

    public static IHostApplicationBuilder AddOnboardingStore(this IHostApplicationBuilder builder)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        builder.AddAzureCosmosContainer(
            ContainerConnectionName,
            configureClientOptions: options => options.UseSystemTextJsonSerializerWithOptions = serializerOptions);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IOnboardingStore, CosmosOnboardingStore>();
        return builder;
    }
}