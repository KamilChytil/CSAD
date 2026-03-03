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

    // Auth
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<UserResponse?> LoginAsync(string email, string password);
    Task LogoutAsync(string token);
    Task<UserResponse?> RegisterExtendedAsync(RegisterRequest request);
    Task<bool> ValidateSessionAsync(Guid sessionId, string token);

    // Children
    Task<List<UserResponse>> GetChildrenAsync(Guid parentId);
    Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password);

    // Account queries
    Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId);

    // Pending transactions
    Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId);
    Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId);
    Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason);

    // Payments
    Task<PaymentDto> SendPaymentAsync(Guid senderAccountId, string recipientAccountNumber, decimal amount, string currency, string? description = null, bool isInstant = false);
    Task<List<PaymentDto>> GetPaymentsByAccountAsync(Guid accountId, int limit = 50);

    // Standing orders
    Task<StandingOrderDto> CreateStandingOrderAsync(Guid senderAccountId, string recipientAccountNumber, decimal amount, string currency, string interval, DateTime firstExecutionDate, string? description = null, DateTime? endDate = null);
    Task<List<StandingOrderDto>> GetStandingOrdersByAccountAsync(Guid accountId);
    Task CancelStandingOrderAsync(Guid standingOrderId);

    // Payment templates
    Task<PaymentTemplateDto> CreatePaymentTemplateAsync(Guid ownerAccountId, string name, string recipientAccountNumber, string currency, string? recipientName = null, decimal? defaultAmount = null, string? defaultDescription = null);
    Task<List<PaymentTemplateDto>> GetPaymentTemplatesByAccountAsync(Guid accountId);
    Task DeletePaymentTemplateAsync(Guid templateId);
}
