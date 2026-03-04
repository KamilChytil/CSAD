using System.Net.Http.Json;
using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Ports;

namespace FairBank.Documents.Infrastructure.HttpClients;

public sealed class ProductsServiceHttpClient(HttpClient httpClient) : IProductsServiceClient
{
    public async Task<ProductContractDto> GetApplicationAsync(Guid applicationId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/products/applications/{applicationId}", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductContractDto>(cancellationToken: ct))!;
    }
}
