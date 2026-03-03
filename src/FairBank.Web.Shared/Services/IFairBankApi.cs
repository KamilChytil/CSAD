using FairBank.Web.Shared.Models;

namespace FairBank.Web.Shared.Services;

public interface IFairBankApi
{
    // Accounts
    Task<AccountResponse?> GetAccountAsync(Guid id);
    Task<AccountResponse> CreateAccountAsync(Guid ownerId, string currency);
    Task<AccountResponse> DepositAsync(Guid accountId, decimal amount, string currency, string? description = null);
    Task<AccountResponse> WithdrawAsync(Guid accountId, decimal amount, string currency, string? description = null);

    // Users
    Task<UserResponse?> GetUserAsync(Guid id);
    Task<UserResponse> RegisterUserAsync(string firstName, string lastName, string email, string password);
}
