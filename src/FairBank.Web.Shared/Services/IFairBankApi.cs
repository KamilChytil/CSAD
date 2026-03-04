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

    // Notifications
    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly = false);
    Task<int> GetUnreadNotificationCountAsync(Guid userId);
    Task MarkNotificationReadAsync(Guid notificationId);
    Task MarkAllNotificationsReadAsync(Guid userId);

    // Spending Limits
    Task SetSpendingLimitAsync(Guid accountId, decimal limit, string currency);

    // Family Chat
    Task GetOrCreateFamilyChatAsync(Guid parentId, Guid childId, string childLabel);

    // Product applications
    Task<ProductApplicationDto> SubmitProductApplicationAsync(Guid userId, string productType, string parameters, decimal monthlyPayment);
    Task<List<ProductApplicationDto>> GetUserApplicationsAsync(Guid userId);
    Task<List<ProductApplicationDto>> GetPendingApplicationsAsync();
    Task<ProductApplicationDto> ApproveApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null);
    Task<ProductApplicationDto> RejectApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null);
    Task<ProductApplicationDto> CancelApplicationAsync(Guid applicationId, Guid userId);

    // Cards
    Task<List<CardDto>> GetCardsByAccountAsync(Guid accountId);
    Task<CardDto?> IssueCardAsync(Guid accountId, string holderName, string type = "Debit");
    Task FreezeCardAsync(Guid cardId);
    Task UnfreezeCardAsync(Guid cardId);
    Task SetCardLimitsAsync(Guid cardId, decimal? dailyLimit, decimal? monthlyLimit, string currency = "CZK");
    Task UpdateCardSettingsAsync(Guid cardId, bool onlinePayments, bool contactless);
    Task DeactivateCardAsync(Guid cardId);

    // Savings Goals
    Task<List<SavingsGoalDto>> GetSavingsGoalsByAccountAsync(Guid accountId);
    Task<SavingsGoalDto?> CreateSavingsGoalAsync(Guid accountId, string name, string? description, decimal targetAmount, string currency = "CZK");
    Task DepositToSavingsGoalAsync(Guid goalId, decimal amount, string currency = "CZK");
    Task WithdrawFromSavingsGoalAsync(Guid goalId, decimal amount, string currency = "CZK");
    Task DeleteSavingsGoalAsync(Guid goalId);

    // Savings Rules
    Task<List<SavingsRuleDto>> GetSavingsRulesByAccountAsync(Guid accountId);
    Task<SavingsRuleDto?> CreateSavingsRuleAsync(Guid accountId, string name, string? description, string type, decimal amount);
    Task ToggleSavingsRuleAsync(Guid ruleId);

    // Investments
    Task<List<InvestmentDto>> GetInvestmentsByAccountAsync(Guid accountId);
    Task<InvestmentDto?> CreateInvestmentAsync(Guid accountId, string name, string type, decimal amount, decimal units, decimal pricePerUnit, string currency = "CZK");
    Task SellInvestmentAsync(Guid investmentId);

    // Admin
    Task<PagedUsersDto?> GetAllUsersAsync(int page = 1, int pageSize = 20, string? role = null, string? search = null);
    Task UpdateUserRoleAsync(Guid userId, string newRole);
    Task DeactivateUserAsync(Guid userId);
    Task ActivateUserAsync(Guid userId);
    Task DeleteUserAsync(Guid userId);

    // Profile
    Task ChangeEmailAsync(Guid userId, string newEmail);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    // 2FA
    Task<TwoFactorSetupResponse?> SetupTwoFactorAsync(Guid userId);
    Task<string[]?> EnableTwoFactorAsync(Guid userId, string code);
    Task DisableTwoFactorAsync(Guid userId, string code);
    Task<LoginResponse?> VerifyTwoFactorAsync(Guid userId, string code);

    // Devices
    Task<List<DeviceDto>> GetDevicesAsync(Guid userId);
    Task RevokeDeviceAsync(Guid deviceId);
    Task TrustDeviceAsync(Guid deviceId);

    // Cards (extended)
    Task SetCardPinAsync(Guid cardId, string pin);

    // Notification preferences
    Task<NotificationPreferenceDto?> GetNotificationPreferencesAsync(Guid userId);
    Task UpdateNotificationPreferencesAsync(Guid userId, NotificationPreferenceDto prefs);

    // Banker clients
    Task<List<BankerClientDto>> GetBankerClientsAsync(Guid bankerId);

    // Exchange
    Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency);
    Task<ExchangeTransactionDto> ExecuteExchangeAsync(ExecuteExchangeRequest request);
    Task<List<ExchangeTransactionDto>> GetExchangeHistoryAsync(Guid userId, int limit = 20);
    Task<List<ExchangeFavoriteDto>> GetExchangeFavoritesAsync(Guid userId);
    Task<ExchangeFavoriteDto> AddExchangeFavoriteAsync(AddFavoriteRequest request);
    Task RemoveExchangeFavoriteAsync(Guid favoriteId);
}
