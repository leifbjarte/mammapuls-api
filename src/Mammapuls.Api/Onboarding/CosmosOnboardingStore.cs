using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace Mammapuls.Api.Onboarding;

public sealed class CosmosOnboardingStore([FromKeyedServices(OnboardingStoreExtensions.ContainerConnectionName)] Container container) : IOnboardingStore
{
    public async Task<OnboardingSubmission?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<OnboardingSubmission>(
                userId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<OnboardingSubmission> UpsertAsync(OnboardingSubmission submission, CancellationToken cancellationToken)
    {
        var response = await container.UpsertItemAsync(
            submission,
            new PartitionKey(submission.UserId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await container.DeleteItemAsync<OnboardingSubmission>(
                userId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
}