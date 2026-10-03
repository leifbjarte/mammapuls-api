using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mammapuls.Api.Users;

public static class UserStoreExtensions
{
    public const string ContainerConnectionName = "users";

    public static IHostApplicationBuilder AddUserStore(this IHostApplicationBuilder builder)
    {
        // System.Text.Json with web defaults (camelCase) so AppUser.Id maps to Cosmos "id".
        builder.AddKeyedAzureCosmosContainer(
            ContainerConnectionName,
            configureClientOptions: options => options.UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web));

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IUserStore, CosmosUserStore>();
        return builder;
    }
}
