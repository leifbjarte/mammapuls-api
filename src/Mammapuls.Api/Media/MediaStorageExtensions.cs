namespace Mammapuls.Api.Media;

public static class MediaStorageExtensions
{
    public const string ContainerConnectionName = "media";

    public static IHostApplicationBuilder AddMediaStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureBlobContainerClient(ContainerConnectionName);
        return builder;
    }
}
