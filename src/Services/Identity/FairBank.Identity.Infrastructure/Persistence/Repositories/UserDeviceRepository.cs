using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserDeviceRepository(IdentityDbContext db) : IUserDeviceRepository
{
    public async Task<IReadOnlyList<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.UserDevices.Where(d => d.UserId == userId).OrderByDescending(d => d.LastActiveAt).ToListAsync(ct);

    public async Task<UserDevice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.UserDevices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<UserDevice?> FindByFingerprintAsync(Guid userId, string browser, string os, string deviceType, CancellationToken ct = default)
        => await db.UserDevices.FirstOrDefaultAsync(d =>
            d.UserId == userId &&
            d.Browser == browser &&
            d.OperatingSystem == os &&
            d.DeviceType == deviceType, ct);

    public async Task AddAsync(UserDevice device, CancellationToken ct = default)
        => await db.UserDevices.AddAsync(device, ct);

    public Task UpdateAsync(UserDevice device, CancellationToken ct = default)
    {
        db.UserDevices.Update(device);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.UserDevices.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
    }
}
