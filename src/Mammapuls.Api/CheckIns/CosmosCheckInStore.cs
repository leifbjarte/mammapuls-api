using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace Mammapuls.Api.CheckIns;

public sealed class CosmosCheckInStore([FromKeyedServices(CheckInStoreExtensions.ContainerConnectionName)] Container container) : ICheckInStore
{
    // The document id is the ISO week ("2026-W40"); the partition key is the user.
    public async Task<CheckInSubmission?> GetAsync(string userId, string week, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<CheckInSubmission>(week, new PartitionKey(userId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CheckInSubmission>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId ORDER BY c.week DESC")
            .WithParameter("@userId", userId);
        using var iterator = container.GetItemQueryIterator<CheckInSubmission>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        var results = new List<CheckInSubmission>();
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        }

        return results;
    }

    public async Task<CheckInSubmission> UpsertAsync(CheckInSubmission submission, CancellationToken cancellationToken)
    {
        var response = await container.UpsertItemAsync(submission, new PartitionKey(submission.UserId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task DeleteAllAsync(string userId, CancellationToken cancellationToken)
    {
        foreach (var submission in await ListAsync(userId, cancellationToken))
        {
            try
            {
                await container.DeleteItemAsync<CheckInSubmission>(submission.Id, new PartitionKey(userId), cancellationToken: cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }
    }
}
