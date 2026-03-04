using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.Domain.Ports;

public interface IUserDeviceRepository
{
    Task<IReadOnlyList<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDevice?> FindByFingerprintAsync(Guid userId, string browser, string os, string deviceType, CancellationToken ct = default);
    Task AddAsync(UserDevice device, CancellationToken ct = default);
    Task UpdateAsync(UserDevice device, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
