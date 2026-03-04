using System.Net.Http.Json;
using FairBank.Web.Shared.Models;

namespace FairBank.Web.Shared.Services;

public sealed class FairBankApiClient(HttpClient http) : IFairBankApi
{
    // ── Accounts ───────────────────────────────────────────────
    public async Task<AccountResponse?> GetAccountAsync(Guid id)
    {
        var response = await http.GetAsync($"api/v1/accounts/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AccountResponse>();
    }

    public async Task<AccountResponse> CreateAccountAsync(Guid ownerId, string currency)
    {
        var response = await http.PostAsJsonAsync("api/v1/accounts", new { OwnerId = ownerId, Currency = currency });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    public async Task<AccountResponse> CreateSavingsAccountAsync(Guid ownerId)
    {
        var response = await http.PostAsJsonAsync("api/v1/accounts", new { OwnerId = ownerId, Currency = "CZK", AccountType = 1 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    public async Task<AccountResponse> DepositAsync(Guid accountId, decimal amount, string currency, string? description = null)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/deposit",
            new { Amount = amount, Currency = currency, Description = description ?? "Vklad" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    public async Task<AccountResponse> WithdrawAsync(Guid accountId, decimal amount, string currency, string? description = null)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/withdraw",
            new { Amount = amount, Currency = currency, Description = description ?? "Výběr" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    // ── Users ──────────────────────────────────────────────────
    public async Task<UserResponse?> GetUserAsync(Guid id)
    {
        var response = await http.GetAsync($"api/v1/users/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse> RegisterUserAsync(string firstName, string lastName, string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/v1/users/register",
            new { FirstName = firstName, LastName = lastName, Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/auth/login", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task LogoutAsync(string token)
    {
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        try
        {
            await http.PostAsync("api/v1/auth/logout", null);
        }
        finally
        {
            http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<UserResponse?> RegisterExtendedAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/auth/register", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, string token)
    {
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        try
        {
            var response = await http.GetAsync($"api/v1/auth/session/{sessionId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
        finally
        {
            http.DefaultRequestHeaders.Authorization = null;
        }
    }

    // ── Children ────────────────────────────────────────────────
    public async Task<List<UserResponse>> GetChildrenAsync(Guid parentId)
    {
        return await http.GetFromJsonAsync<List<UserResponse>>($"api/v1/users/{parentId}/children") ?? [];
    }

    public async Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password)
    {
        var response = await http.PostAsJsonAsync($"api/v1/users/{parentId}/children",
            new { FirstName = firstName, LastName = lastName, Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    // ── Account queries ─────────────────────────────────────────
    public async Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId)
    {
        return await http.GetFromJsonAsync<List<AccountResponse>>($"api/v1/accounts?ownerId={ownerId}") ?? [];
    }

    // ── Pending transactions ────────────────────────────────────
    public async Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId)
    {
        return await http.GetFromJsonAsync<List<PendingTransactionDto>>($"api/v1/accounts/{accountId}/pending") ?? [];
    }

    public async Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/pending/{transactionId}/approve",
            new { ApproverId = approverId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }

    public async Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/pending/{transactionId}/reject",
            new { ApproverId = approverId, Reason = reason });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }

    // ── Login (from main) ───────────────────────────────────────
    public async Task<UserResponse?> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/v1/users/login",
            new { Email = email, Password = password });

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    // ── Payments ─────────────────────────────────────────────────
    public async Task<PaymentDto> SendPaymentAsync(Guid senderAccountId, string recipientAccountNumber, decimal amount, string currency, string? description = null, bool isInstant = false)
    {
        var response = await http.PostAsJsonAsync("api/v1/payments",
            new { SenderAccountId = senderAccountId, RecipientAccountNumber = recipientAccountNumber, Amount = amount, Currency = currency, Description = description, IsInstant = isInstant });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            throw new InvalidOperationException(body?.Error ?? "Platba se nezdařila.");
        }
        return (await response.Content.ReadFromJsonAsync<PaymentDto>())!;
    }

    public async Task<List<PaymentDto>> GetPaymentsByAccountAsync(Guid accountId, int limit = 50)
    {
        return await http.GetFromJsonAsync<List<PaymentDto>>($"api/v1/payments/account/{accountId}?limit={limit}") ?? [];
    }

    // ── Standing orders ─────────────────────────────────────────
    public async Task<StandingOrderDto> CreateStandingOrderAsync(Guid senderAccountId, string recipientAccountNumber, decimal amount, string currency, string interval, DateTime firstExecutionDate, string? description = null, DateTime? endDate = null)
    {
        var response = await http.PostAsJsonAsync("api/v1/standing-orders",
            new { SenderAccountId = senderAccountId, RecipientAccountNumber = recipientAccountNumber, Amount = amount, Currency = currency, Interval = interval, FirstExecutionDate = firstExecutionDate, Description = description, EndDate = endDate });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            throw new InvalidOperationException(body?.Error ?? "Trvaly příkaz se nepodařilo vytvořit.");
        }
        return (await response.Content.ReadFromJsonAsync<StandingOrderDto>())!;
    }

    public async Task<List<StandingOrderDto>> GetStandingOrdersByAccountAsync(Guid accountId)
    {
        return await http.GetFromJsonAsync<List<StandingOrderDto>>($"api/v1/standing-orders/account/{accountId}") ?? [];
    }

    public async Task CancelStandingOrderAsync(Guid standingOrderId)
    {
        var response = await http.DeleteAsync($"api/v1/standing-orders/{standingOrderId}");
        response.EnsureSuccessStatusCode();
    }

    // ── Payment templates ───────────────────────────────────────
    public async Task<PaymentTemplateDto> CreatePaymentTemplateAsync(Guid ownerAccountId, string name, string recipientAccountNumber, string currency, string? recipientName = null, decimal? defaultAmount = null, string? defaultDescription = null)
    {
        var response = await http.PostAsJsonAsync("api/v1/payment-templates",
            new { OwnerAccountId = ownerAccountId, Name = name, RecipientAccountNumber = recipientAccountNumber, Currency = currency, RecipientName = recipientName, DefaultAmount = defaultAmount, DefaultDescription = defaultDescription });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentTemplateDto>())!;
    }

    public async Task<List<PaymentTemplateDto>> GetPaymentTemplatesByAccountAsync(Guid accountId)
    {
        return await http.GetFromJsonAsync<List<PaymentTemplateDto>>($"api/v1/payment-templates/account/{accountId}") ?? [];
    }

    public async Task DeletePaymentTemplateAsync(Guid templateId)
    {
        var response = await http.DeleteAsync($"api/v1/payment-templates/{templateId}");
        response.EnsureSuccessStatusCode();
    }

    // ── Notifications ──────────────────────────────────────────
    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly = false)
    {
        var url = $"api/v1/notifications?userId={userId}&unreadOnly={unreadOnly}";
        return await http.GetFromJsonAsync<List<NotificationDto>>(url) ?? [];
    }

    public async Task<int> GetUnreadNotificationCountAsync(Guid userId)
    {
        var result = await http.GetFromJsonAsync<UnreadCountDto>($"api/v1/notifications/unread-count?userId={userId}");
        return result?.Count ?? 0;
    }

    public async Task MarkNotificationReadAsync(Guid notificationId)
    {
        await http.PostAsync($"api/v1/notifications/{notificationId}/read", null);
    }

    public async Task MarkAllNotificationsReadAsync(Guid userId)
    {
        await http.PostAsync($"api/v1/notifications/read-all?userId={userId}", null);
    }

    // ── Spending Limits ──────────────────────────────────────────
    public async Task SetSpendingLimitAsync(Guid accountId, decimal limit, string currency)
    {
        await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/limits",
            new { Limit = limit, Currency = currency });
    }

    // ── Family Chat ──────────────────────────────────────────────
    public async Task GetOrCreateFamilyChatAsync(Guid parentId, Guid childId, string childLabel)
    {
        await http.PostAsync(
            $"api/v1/chat/conversations/family?parentId={parentId}&childId={childId}&childLabel={Uri.EscapeDataString(childLabel)}", null);
    }

    // ── Product applications ──────────────────────────────────
    public async Task<ProductApplicationDto> SubmitProductApplicationAsync(Guid userId, string productType, string parameters, decimal monthlyPayment)
    {
        var response = await http.PostAsJsonAsync("api/v1/products/applications",
            new { UserId = userId, ProductType = productType, Parameters = parameters, MonthlyPayment = monthlyPayment });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
    }

    public async Task<List<ProductApplicationDto>> GetUserApplicationsAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<List<ProductApplicationDto>>($"api/v1/products/applications/user/{userId}") ?? [];
    }

    public async Task<List<ProductApplicationDto>> GetPendingApplicationsAsync()
    {
        return await http.GetFromJsonAsync<List<ProductApplicationDto>>("api/v1/products/applications/pending") ?? [];
    }

    public async Task<ProductApplicationDto> ApproveApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null)
    {
        var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/approve",
            new { ApplicationId = applicationId, ReviewerId = reviewerId, Note = note });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
    }

    public async Task<ProductApplicationDto> RejectApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null)
    {
        var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/reject",
            new { ApplicationId = applicationId, ReviewerId = reviewerId, Note = note });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
    }

    public async Task<ProductApplicationDto> CancelApplicationAsync(Guid applicationId, Guid userId)
    {
        var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/cancel",
            new { ApplicationId = applicationId, UserId = userId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
    }

    // ── Cards ──────────────────────────────────────────────────
    public async Task<List<CardDto>> GetCardsByAccountAsync(Guid accountId)
    {
        var response = await http.GetAsync($"api/v1/accounts/{accountId}/cards");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CardDto>>() ?? [];
    }

    public async Task<CardDto?> IssueCardAsync(Guid accountId, string holderName, string type = "Debit")
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/cards",
            new { HolderName = holderName, Type = type });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CardDto>();
    }

    public async Task FreezeCardAsync(Guid cardId)
    {
        await http.PostAsync($"api/v1/cards/{cardId}/freeze", null);
    }

    public async Task UnfreezeCardAsync(Guid cardId)
    {
        await http.PostAsync($"api/v1/cards/{cardId}/unfreeze", null);
    }

    public async Task SetCardLimitsAsync(Guid cardId, decimal? dailyLimit, decimal? monthlyLimit, string currency = "CZK")
    {
        await http.PutAsJsonAsync($"api/v1/cards/{cardId}/limits",
            new { DailyLimit = dailyLimit, MonthlyLimit = monthlyLimit, Currency = currency });
    }

    public async Task UpdateCardSettingsAsync(Guid cardId, bool onlinePayments, bool contactless)
    {
        await http.PutAsJsonAsync($"api/v1/cards/{cardId}/settings",
            new { OnlinePaymentsEnabled = onlinePayments, ContactlessEnabled = contactless });
    }

    public async Task DeactivateCardAsync(Guid cardId)
    {
        await http.DeleteAsync($"api/v1/cards/{cardId}");
    }

    // ── Savings Goals ─────────────────────────────────────────
    public async Task<List<SavingsGoalDto>> GetSavingsGoalsByAccountAsync(Guid accountId)
    {
        var response = await http.GetAsync($"api/v1/accounts/{accountId}/savings-goals");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<SavingsGoalDto>>() ?? [];
    }

    public async Task<SavingsGoalDto?> CreateSavingsGoalAsync(Guid accountId, string name, string? description, decimal targetAmount, string currency = "CZK")
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/savings-goals",
            new { Name = name, Description = description, TargetAmount = targetAmount, Currency = currency });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SavingsGoalDto>();
    }

    public async Task DepositToSavingsGoalAsync(Guid goalId, decimal amount, string currency = "CZK")
    {
        await http.PostAsJsonAsync($"api/v1/savings-goals/{goalId}/deposit",
            new { Amount = amount, Currency = currency });
    }

    public async Task WithdrawFromSavingsGoalAsync(Guid goalId, decimal amount, string currency = "CZK")
    {
        await http.PostAsJsonAsync($"api/v1/savings-goals/{goalId}/withdraw",
            new { Amount = amount, Currency = currency });
    }

    public async Task DeleteSavingsGoalAsync(Guid goalId)
    {
        await http.DeleteAsync($"api/v1/savings-goals/{goalId}");
    }

    // ── Savings Rules ─────────────────────────────────────────
    public async Task<List<SavingsRuleDto>> GetSavingsRulesByAccountAsync(Guid accountId)
    {
        var response = await http.GetAsync($"api/v1/accounts/{accountId}/savings-rules");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<SavingsRuleDto>>() ?? [];
    }

    public async Task<SavingsRuleDto?> CreateSavingsRuleAsync(Guid accountId, string name, string? description, string type, decimal amount)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/savings-rules",
            new { Name = name, Description = description, Type = type, Amount = amount });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SavingsRuleDto>();
    }

    public async Task ToggleSavingsRuleAsync(Guid ruleId)
    {
        await http.PutAsync($"api/v1/savings-rules/{ruleId}/toggle", null);
    }

    // ── Investments ───────────────────────────────────────────
    public async Task<List<InvestmentDto>> GetInvestmentsByAccountAsync(Guid accountId)
    {
        var response = await http.GetAsync($"api/v1/accounts/{accountId}/investments");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<InvestmentDto>>() ?? [];
    }

    public async Task<InvestmentDto?> CreateInvestmentAsync(Guid accountId, string name, string type, decimal amount, decimal units, decimal pricePerUnit, string currency = "CZK")
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/{accountId}/investments",
            new { Name = name, Type = type, Amount = amount, Units = units, PricePerUnit = pricePerUnit, Currency = currency });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InvestmentDto>();
    }

    public async Task SellInvestmentAsync(Guid investmentId)
    {
        await http.PostAsync($"api/v1/investments/{investmentId}/sell", null);
    }

    // ── Admin ─────────────────────────────────────────────────
    public async Task<PagedUsersDto?> GetAllUsersAsync(int page = 1, int pageSize = 20, string? role = null, string? search = null)
    {
        var url = $"api/v1/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(role)) url += $"&role={Uri.EscapeDataString(role)}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PagedUsersDto>();
    }

    public async Task UpdateUserRoleAsync(Guid userId, string newRole)
    {
        await http.PutAsJsonAsync($"api/v1/users/{userId}/role",
            new { NewRole = newRole });
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        await http.PostAsync($"api/v1/users/{userId}/deactivate", null);
    }

    public async Task ActivateUserAsync(Guid userId)
    {
        await http.PostAsync($"api/v1/users/{userId}/activate", null);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        await http.DeleteAsync($"api/v1/users/{userId}");
    }

    public async Task<PagedAuditLogsResponse> GetAuditLogsAsync(int page = 1, int pageSize = 20)
    {
        var url = $"api/v1/users/admin/audit-logs?page={page}&pageSize={pageSize}";
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedAuditLogsResponse>())!;
    }

    // ── Profile ───────────────────────────────────────────────
    public async Task ChangeEmailAsync(Guid userId, string newEmail)
    {
        await http.PutAsJsonAsync($"api/v1/users/{userId}/email",
            new { NewEmail = newEmail });
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        await http.PutAsJsonAsync($"api/v1/users/{userId}/password",
            new { CurrentPassword = currentPassword, NewPassword = newPassword });
    }

    // ── 2FA ──────────────────────────────────────────────────
    public async Task<TwoFactorSetupResponse?> SetupTwoFactorAsync(Guid userId)
    {
        var response = await http.PostAsync($"api/v1/users/2fa/setup?userId={userId}", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
    }

    public async Task<string[]?> EnableTwoFactorAsync(Guid userId, string code)
    {
        var response = await http.PostAsJsonAsync("api/v1/users/2fa/enable",
            new { UserId = userId, Code = code });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<string[]>();
    }

    public async Task DisableTwoFactorAsync(Guid userId, string code)
    {
        var response = await http.PostAsJsonAsync("api/v1/users/2fa/disable",
            new { UserId = userId, Code = code });
        response.EnsureSuccessStatusCode();
    }

    public async Task<LoginResponse?> VerifyTwoFactorAsync(Guid userId, string code)
    {
        var response = await http.PostAsJsonAsync("api/v1/users/2fa/verify",
            new { UserId = userId, Code = code });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    // ── Devices ─────────────────────────────────────────────
    public async Task<List<DeviceDto>> GetDevicesAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<List<DeviceDto>>($"api/v1/users/{userId}/devices") ?? [];
    }

    public async Task RevokeDeviceAsync(Guid deviceId)
    {
        await http.DeleteAsync($"api/v1/users/devices/{deviceId}");
    }

    public async Task TrustDeviceAsync(Guid deviceId)
    {
        await http.PostAsync($"api/v1/users/devices/{deviceId}/trust", null);
    }

    // ── Cards (extended) ────────────────────────────────────
    public async Task SetCardPinAsync(Guid cardId, string pin)
    {
        await http.PutAsJsonAsync($"api/v1/cards/{cardId}/pin",
            new { Pin = pin });
    }

    // ── Notification preferences ────────────────────────────
    public async Task<NotificationPreferenceDto?> GetNotificationPreferencesAsync(Guid userId)
    {
        var response = await http.GetAsync($"api/v1/notifications/preferences?userId={userId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<NotificationPreferenceDto>();
    }

    public async Task UpdateNotificationPreferencesAsync(Guid userId, NotificationPreferenceDto prefs)
    {
        await http.PutAsJsonAsync($"api/v1/notifications/preferences?userId={userId}", prefs);
    }

    // ── Banker clients ─────────────────────────────────────
    public async Task<List<BankerClientDto>> GetBankerClientsAsync(Guid bankerId)
    {
        var response = await http.GetAsync($"api/v1/chat/conversations/banker/{bankerId}/clients");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<BankerClientDto>>() ?? [];
    }

    private sealed record UnreadCountDto(int Count);
    private sealed record ErrorBody(string? Error);
}
