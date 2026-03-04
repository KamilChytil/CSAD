using System.Net.Http.Json;
using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Ports;

namespace FairBank.Documents.Infrastructure.HttpClients;

public sealed class AccountsServiceHttpClient(HttpClient httpClient) : IAccountsServiceClient
{
    public async Task<IReadOnlyList<DocumentTransactionDto>> GetTransactionsAsync(Guid accountId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var url = $"api/v1/accounts/{accountId}/transactions";
        var query = new List<string>();
        if (from.HasValue) query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("o")));
        if (to.HasValue) query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("o")));
        if (query.Count > 0) url += "?" + string.Join("&", query);

        var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<DocumentTransactionDto>();
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<DocumentTransactionDto>>(cancellationToken: ct);
        return list ?? Array.Empty<DocumentTransactionDto>();
    }
}
