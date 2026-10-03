namespace Mammapuls.Api.Onboarding;

public interface IOnboardingStore
{
    Task<OnboardingSubmission?> GetAsync(string userId, CancellationToken cancellationToken);
    Task<OnboardingSubmission> UpsertAsync(OnboardingSubmission submission, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, CancellationToken cancellationToken);
}