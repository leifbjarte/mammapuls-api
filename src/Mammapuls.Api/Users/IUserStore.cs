namespace Mammapuls.Api.Users;

public interface IUserStore
{
    Task<AppUser> UpsertFromLoginAsync(string issuer, string subject, string? name, string? email, string? phoneNumber, CancellationToken cancellationToken);
    Task<AppUser?> GetAsync(string id, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
