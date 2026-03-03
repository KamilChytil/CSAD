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
}
