using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.GetPaymentStatistics;

public sealed class GetPaymentStatisticsQueryHandler(
    IPaymentRepository paymentRepository) : IRequestHandler<GetPaymentStatisticsQuery, PaymentStatisticsResponse>
{
    public async Task<PaymentStatisticsResponse> Handle(GetPaymentStatisticsQuery request, CancellationToken ct)
    {
        // Load all payments for the account within the date range
        var (allPayments, _) = await paymentRepository.SearchAsync(
            request.AccountId,
            request.DateFrom,
            request.DateTo,
            minAmount: null,
            maxAmount: null,
            category: null,
            status: PaymentStatus.Completed,
            searchText: null,
            page: 1,
            pageSize: int.MaxValue,
            sortBy: "CreatedAt",
            sortDirection: "desc",
            ct);

        var accountId = request.AccountId;

        // Separate income (received) vs expenses (sent)
        var income = allPayments
            .Where(p => p.RecipientAccountId == accountId)
            .ToList();

        var expenses = allPayments
            .Where(p => p.SenderAccountId == accountId)
            .ToList();

        var totalIncome = income.Sum(p => p.Amount);
        var totalExpenses = expenses.Sum(p => p.Amount);
        var transactionCount = allPayments.Count;
        var averageTransaction = transactionCount > 0
            ? Math.Round((totalIncome + totalExpenses) / transactionCount, 2)
            : 0m;

        // Category breakdown (expenses only)
        var totalExpenseAmount = totalExpenses > 0 ? totalExpenses : 1m; // avoid division by zero
        var categoryBreakdown = expenses
            .GroupBy(p => p.Category)
            .Select(g => new CategoryBreakdown(
                g.Key.ToString(),
                g.Sum(p => p.Amount),
                Math.Round((double)(g.Sum(p => p.Amount) / totalExpenseAmount) * 100, 1)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Monthly trend
        var monthlyTrend = allPayments
            .GroupBy(p => p.CreatedAt.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyTrend(
                g.Key,
                g.Where(p => p.RecipientAccountId == accountId).Sum(p => p.Amount),
                g.Where(p => p.SenderAccountId == accountId).Sum(p => p.Amount)))
            .ToList();

        return new PaymentStatisticsResponse(
            totalIncome,
            totalExpenses,
            transactionCount,
            averageTransaction,
            categoryBreakdown,
            monthlyTrend);
    }
}
