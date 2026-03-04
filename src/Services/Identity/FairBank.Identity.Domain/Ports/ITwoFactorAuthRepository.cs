using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.Domain.Ports;

public interface ITwoFactorAuthRepository
{
    Task<TwoFactorAuth?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default);
    Task UpdateAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
