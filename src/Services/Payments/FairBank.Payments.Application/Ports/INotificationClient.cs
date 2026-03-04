namespace FairBank.Payments.Application.Ports;

public interface INotificationClient
{
    Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default);
}
