namespace FairBank.Payments.Application.Ports;

public interface IIdentityClient
{
    Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserInfo(Guid Id, string FirstName, string LastName, string Role, Guid? ParentId);
