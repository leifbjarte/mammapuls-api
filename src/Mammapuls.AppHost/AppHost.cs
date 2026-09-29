using Azure.Provisioning.AppContainers;
using Azure.Provisioning.CosmosDB;
using Azure.Provisioning.OperationalInsights;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca")
    .WithDashboard(false)
    .ConfigureInfrastructure(infra =>
    {
        var workspace = infra.GetProvisionableResources().OfType<OperationalInsightsWorkspace>().Single();
        workspace.RetentionInDays = 30;
        workspace.WorkspaceCapping = new OperationalInsightsWorkspaceCapping { DailyQuotaInGB = 0.15 };
    });

#pragma warning disable ASPIRECOSMOSDB001 // Linux preview emulator is experimental
var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsPreviewEmulator(emulator => emulator.WithDataExplorer())
    .ConfigureInfrastructure(infra =>
    {
        var resources = infra.GetProvisionableResources();

        // Free tier can only be set at account creation and is incompatible with serverless (Aspire's default).
        var account = resources.OfType<CosmosDBAccount>().Single();
        account.Capabilities.Clear();
        account.IsFreeTierEnabled = true;

        // Shared database throughput stays inside the free 1000 RU/s.
        var database = resources.OfType<CosmosDBSqlDatabase>().Single();
        database.Options = new CosmosDBCreateUpdateConfig { Throughput = 1000 };
    });
#pragma warning restore ASPIRECOSMOSDB001

var users = cosmos.AddCosmosDatabase("mammapuls")
    .AddContainer("users", "/id");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .ConfigureInfrastructure(infra =>
    {
        var account = infra.GetProvisionableResources().OfType<StorageAccount>().Single();
        account.Sku = new StorageSku { Name = StorageSkuName.StandardLrs };
        account.AllowBlobPublicAccess = false;
    });

var media = storage.AddBlobContainer("media");
var dataProtection = storage.AddBlobContainer("dataprotection");

var vippsClientId = builder.AddParameter("vipps-client-id");
var vippsClientSecret = builder.AddParameter("vipps-client-secret", secret: true);
var spaOrigin = builder.AddParameter("spa-origin");

builder.AddProject<Projects.Mammapuls_Api>("api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(users).WaitFor(users)
    .WithReference(media).WaitFor(media)
    .WithReference(dataProtection).WaitFor(dataProtection)
    // Delegator is needed for user-delegation SAS on media.
    .WithRoleAssignments(storage, StorageBuiltInRole.StorageBlobDataContributor, StorageBuiltInRole.StorageBlobDelegator)
    .WithEnvironment("Authentication__Vipps__ClientId", vippsClientId)
    .WithEnvironment("Authentication__Vipps__ClientSecret", vippsClientSecret)
    .WithEnvironment("Cors__AllowedOrigins__0", spaOrigin)
    .PublishAsAzureContainerApp((_, app) =>
    {
        app.Template.Scale = new ContainerAppScale { MinReplicas = 0, MaxReplicas = 2 };
        app.Template.Containers.Single().Value!.Resources = new AppContainerResources { Cpu = 0.25, Memory = "0.5Gi" };
        // The API persists DataProtection keys to the `dataprotection` blob container; drop ACA's platform key ring so there is one source.
        app.Configuration.ProvisionableProperties.Remove("AutoConfigureDataProtection");
    });

builder.Build().Run();
