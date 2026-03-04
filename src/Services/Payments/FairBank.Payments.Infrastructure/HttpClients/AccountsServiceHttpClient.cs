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
        var response = await httpClient.GetAsync($"api/v1/accounts/by-number?accountNumber={Uri.EscapeDataString(accountNumber)}", ct);
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

    public async Task<SpendingLimitInfo?> GetSpendingLimitAsync(Guid accountId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/accounts/{accountId}/limits", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<SpendingLimitApiDto>(ct);
        return dto is null ? null : new SpendingLimitInfo(dto.RequiresApproval, dto.ApprovalThreshold, dto.Currency);
    }

    public async Task<AccountLimitsInfo?> GetAccountLimitsAsync(Guid accountId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/accounts/{accountId}/limits", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<AccountLimitsApiDto>(ct);
        return dto is null
            ? null
            : new AccountLimitsInfo(
                dto.DailyTransactionLimit,
                dto.MonthlyTransactionLimit,
                dto.SingleTransactionLimit,
                dto.DailyTransactionCount,
                dto.OnlinePaymentLimit);
    }

    public async Task<PendingTransactionInfo?> CreatePendingTransactionAsync(
        Guid accountId, decimal amount, string currency, string description, Guid requestedBy, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/v1/accounts/pending",
            new { AccountId = accountId, Amount = amount, Currency = currency, Description = description, RequestedBy = requestedBy }, ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<PendingTxApiDto>(ct);
        return dto is null ? null : new PendingTransactionInfo(dto.Id, dto.Status);
    }

    private sealed record AccountApiDto(Guid Id, Guid OwnerId, string AccountNumber, decimal Balance, string Currency, bool IsActive, DateTime CreatedAt);
    private sealed record SpendingLimitApiDto(bool RequiresApproval, decimal? ApprovalThreshold, string? Currency, decimal? SpendingLimit);
    private sealed record AccountLimitsApiDto(
        decimal DailyTransactionLimit, decimal MonthlyTransactionLimit, decimal SingleTransactionLimit,
        int DailyTransactionCount, decimal OnlinePaymentLimit, decimal DailyUsed, decimal MonthlyUsed, int DailyCountUsed);
    private sealed record PendingTxApiDto(Guid Id, string Status);
}
