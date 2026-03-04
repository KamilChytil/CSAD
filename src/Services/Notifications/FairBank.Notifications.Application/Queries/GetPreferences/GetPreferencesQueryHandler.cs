using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Queries.GetPreferences;

public sealed class GetPreferencesQueryHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<GetPreferencesQuery, NotificationPreferenceResponse>
{
    public async Task<NotificationPreferenceResponse> Handle(GetPreferencesQuery request, CancellationToken ct)
    {
        var preference = await repository.GetByUserIdAsync(request.UserId, ct);

        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(request.UserId);
            await repository.AddAsync(preference, ct);
        }

        return new NotificationPreferenceResponse(
            preference.Id,
            preference.UserId,
            preference.TransactionNotifications,
            preference.SecurityNotifications,
            preference.CardNotifications,
            preference.LimitNotifications,
            preference.ChatNotifications,
            preference.EmailNotificationsEnabled,
            preference.PushNotificationsEnabled);
    }
}
