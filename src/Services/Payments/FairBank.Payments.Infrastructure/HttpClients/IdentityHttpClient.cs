using System.Net.Http.Json;
using FairBank.Payments.Application.Ports;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class IdentityHttpClient(HttpClient httpClient) : IIdentityClient
{
    public async Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/users/{userId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<UserApiDto>(ct);
        return dto is null ? null : new UserInfo(dto.Id, dto.FirstName, dto.LastName, dto.Role, dto.ParentId);
    }

    private sealed record UserApiDto(Guid Id, string FirstName, string LastName, string Role, Guid? ParentId);
}
