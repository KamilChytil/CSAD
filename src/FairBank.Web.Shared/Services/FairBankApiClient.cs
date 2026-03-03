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
        response.EnsureSuccessStatusCode();
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
        response.EnsureSuccessStatusCode();
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
}
