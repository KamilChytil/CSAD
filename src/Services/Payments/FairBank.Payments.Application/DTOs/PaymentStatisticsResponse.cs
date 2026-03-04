namespace FairBank.Payments.Application.DTOs;

public sealed record PaymentStatisticsResponse(
    decimal TotalIncome,
    decimal TotalExpenses,
    int TransactionCount,
    decimal AverageTransaction,
    IReadOnlyList<CategoryBreakdown> CategoryBreakdown,
    IReadOnlyList<MonthlyTrend> MonthlyTrend);

public sealed record CategoryBreakdown(
    string Category,
    decimal Amount,
    double Percentage);

public sealed record MonthlyTrend(
    string Month,
    decimal Income,
    decimal Expenses);
