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
}

public sealed record AccountInfo(Guid Id, Guid OwnerId, string AccountNumber, decimal Balance, string Currency, bool IsActive);
