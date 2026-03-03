using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Ports;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken ct = default);
}
