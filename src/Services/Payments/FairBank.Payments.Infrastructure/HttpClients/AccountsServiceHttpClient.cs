using System.Net.Http.Json;
using FairBank.Payments.Application.Ports;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class AccountsServiceHttpClient(HttpClient httpClient) : IAccountsServiceClient
{
    public async Task<AccountInfo?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/accounts/{accountId}", ct);
        if (!response.IsSuccessStatusCode) return null;

        var dto = await response.Content.ReadFromJsonAsync<AccountApiDto>(ct);
        return dto is null ? null : new AccountInfo(dto.Id, dto.OwnerId, dto.AccountNumber, dto.Balance, dto.Currency, dto.IsActive);
    }

    public async Task<AccountInfo?> GetAccountByNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/accounts/by-number/{Uri.EscapeDataString(accountNumber)}", ct);
        if (!response.IsSuccessStatusCode) return null;

        var dto = await response.Content.ReadFromJsonAsync<AccountApiDto>(ct);
        return dto is null ? null : new AccountInfo(dto.Id, dto.OwnerId, dto.AccountNumber, dto.Balance, dto.Currency, dto.IsActive);
    }

    public async Task<bool> WithdrawAsync(Guid accountId, decimal amount, string currency, string description, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/v1/accounts/{accountId}/withdraw",
            new { Amount = amount, Currency = currency, Description = description }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DepositAsync(Guid accountId, decimal amount, string currency, string description, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/v1/accounts/{accountId}/deposit",
            new { Amount = amount, Currency = currency, Description = description }, ct);
        return response.IsSuccessStatusCode;
    }

    private sealed record AccountApiDto(Guid Id, Guid OwnerId, string AccountNumber, decimal Balance, string Currency, bool IsActive, DateTime CreatedAt);
}
