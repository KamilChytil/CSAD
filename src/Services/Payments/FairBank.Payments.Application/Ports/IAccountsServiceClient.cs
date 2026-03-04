namespace FairBank.Payments.Application.Ports;

/// <summary>
/// Port for communicating with the Accounts microservice via HTTP.
/// </summary>
public interface IAccountsServiceClient
{
    Task<AccountInfo?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<AccountInfo?> GetAccountByNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<bool> WithdrawAsync(Guid accountId, decimal amount, string currency, string description, CancellationToken ct = default);
    Task<bool> DepositAsync(Guid accountId, decimal amount, string currency, string description, CancellationToken ct = default);
    Task<SpendingLimitInfo?> GetSpendingLimitAsync(Guid accountId, CancellationToken ct = default);
    Task<AccountLimitsInfo?> GetAccountLimitsAsync(Guid accountId, CancellationToken ct = default);
    Task<PendingTransactionInfo?> CreatePendingTransactionAsync(Guid accountId, decimal amount, string currency, string description, Guid requestedBy, CancellationToken ct = default);
}

public sealed record AccountInfo(Guid Id, Guid OwnerId, string AccountNumber, decimal Balance, string Currency, bool IsActive);
public sealed record SpendingLimitInfo(bool RequiresApproval, decimal? ApprovalThreshold, string? Currency);
public sealed record AccountLimitsInfo(
    decimal DailyTransactionLimit,
    decimal MonthlyTransactionLimit,
    decimal SingleTransactionLimit,
    int DailyTransactionCount,
    decimal OnlinePaymentLimit);
public sealed record PendingTransactionInfo(Guid Id, string Status);
