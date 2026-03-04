using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IPaymentRepository : IRepository<Payment, Guid>
{
    Task<IReadOnlyList<Payment>> GetByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetSentByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetReceivedByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);

    Task<(IReadOnlyList<Payment> Items, int TotalCount)> SearchAsync(
        Guid accountId,
        DateTime? dateFrom,
        DateTime? dateTo,
        decimal? minAmount,
        decimal? maxAmount,
        PaymentCategory? category,
        PaymentStatus? status,
        string? searchText,
        int page,
        int pageSize,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default);

    Task<(decimal TotalAmount, int Count)> GetSentTotalsAsync(
        Guid senderAccountId,
        DateTime from,
        CancellationToken ct = default);
}
