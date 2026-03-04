using System.Net.Http.Json;
using FairBank.Payments.Application.Ports;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class NotificationHttpClient(HttpClient httpClient) : INotificationClient
{
    public async Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default)
    {
        try
        {
            await httpClient.PostAsJsonAsync("api/v1/notifications",
                new { UserId = userId, Type = type, Title = title, Message = message,
                      RelatedEntityId = relatedEntityId, RelatedEntityType = relatedEntityType }, ct);
        }
        catch { /* Fire and forget — notification failure should not block payment */ }
    }
}
