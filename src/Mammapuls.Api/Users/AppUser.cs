namespace Mammapuls.Api.Users;

public sealed record AppUser(string Id, string Issuer, string Subject, string? Name, string? Email, string? PhoneNumber, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
