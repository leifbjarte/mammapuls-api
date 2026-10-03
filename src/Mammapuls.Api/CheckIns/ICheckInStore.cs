namespace Mammapuls.Api.CheckIns;

public interface ICheckInStore
{
    Task<CheckInSubmission?> GetAsync(string userId, string week, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckInSubmission>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<CheckInSubmission> UpsertAsync(CheckInSubmission submission, CancellationToken cancellationToken);
    Task DeleteAllAsync(string userId, CancellationToken cancellationToken);
}
