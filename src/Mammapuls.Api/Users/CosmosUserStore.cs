using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Cosmos;

namespace Mammapuls.Api.Users;

public sealed class CosmosUserStore(Container container, TimeProvider timeProvider) : IUserStore
{
    // Deterministic, URL-safe, fixed-length (43 chars) id; also the partition key.
    public static string CreateId(string issuer, string subject)
    {
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        ArgumentException.ThrowIfNullOrEmpty(subject);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes($"{issuer}|{subject}")));
    }

    public async Task<AppUser> UpsertFromLoginAsync(string issuer, string subject, string? name, string? email, string? phoneNumber, CancellationToken cancellationToken)
    {
        var id = CreateId(issuer, subject);
        var partitionKey = new PartitionKey(id);

        // Patch keeps CreatedAt intact for returning users; create covers first login, and a lost create race falls back to patch.
        for (var attempt = 0; ; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            try
            {
                var patched = await container.PatchItemAsync<AppUser>(
                    id,
                    partitionKey,
                    [
                        PatchOperation.Set("/name", name),
                        PatchOperation.Set("/email", email),
                        PatchOperation.Set("/phoneNumber", phoneNumber),
                        PatchOperation.Set("/updatedAt", now),
                    ],
                    cancellationToken: cancellationToken);
                return patched.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }

            try
            {
                var created = await container.CreateItemAsync(
                    new AppUser(id, issuer, subject, name, email, phoneNumber, now, now),
                    partitionKey,
                    cancellationToken: cancellationToken);
                return created.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict && attempt < 2)
            {
            }
        }
    }

    public async Task<AppUser?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<AppUser>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await container.DeleteItemAsync<AppUser>(id, new PartitionKey(id), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
}
