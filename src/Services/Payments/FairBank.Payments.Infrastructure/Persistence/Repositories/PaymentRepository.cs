using System.Linq.Expressions;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentsDbContext context) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Payment aggregate, CancellationToken ct = default)
        => await context.Payments.AddAsync(aggregate, ct);

    public Task UpdateAsync(Payment aggregate, CancellationToken ct = default)
    {
        context.Payments.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Payment>> GetByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.SenderAccountId == accountId || p.RecipientAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> GetSentByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.SenderAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> GetReceivedByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.RecipientAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Payment> Items, int TotalCount)> SearchAsync(
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
        CancellationToken ct = default)
    {
        var query = context.Payments
            .Where(p => p.SenderAccountId == accountId || p.RecipientAccountId == accountId);

        if (dateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= dateTo.Value);

        if (minAmount.HasValue)
            query = query.Where(p => p.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(p => p.Amount <= maxAmount.Value);

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(p => p.Description != null && p.Description.Contains(searchText));

        var totalCount = await query.CountAsync(ct);

        var keySelector = GetSortExpression(sortBy);
        query = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(decimal TotalAmount, int Count)> GetSentTotalsAsync(
        Guid senderAccountId, DateTime from, CancellationToken ct = default)
    {
        var query = context.Payments
            .Where(p => p.SenderAccountId == senderAccountId
                        && p.CreatedAt >= from
                        && p.Status == PaymentStatus.Completed);

        var totalAmount = await query.SumAsync(p => p.Amount, ct);
        var count = await query.CountAsync(ct);

        return (totalAmount, count);
    }

    private static Expression<Func<Payment, object>> GetSortExpression(string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "amount" => p => p.Amount,
        "status" => p => p.Status,
        "category" => p => p.Category,
        "type" => p => p.Type,
        _ => p => p.CreatedAt
    };
}
