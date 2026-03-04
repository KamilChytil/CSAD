using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Commands.UpdatePreferences;

public sealed class UpdatePreferencesCommandHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<UpdatePreferencesCommand, NotificationPreferenceResponse>
{
    public async Task<NotificationPreferenceResponse> Handle(UpdatePreferencesCommand request, CancellationToken ct)
    {
        var preference = await repository.GetByUserIdAsync(request.UserId, ct);

        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(request.UserId);
            preference.Update(
                request.TransactionNotifications,
                request.SecurityNotifications,
                request.CardNotifications,
                request.LimitNotifications,
                request.ChatNotifications,
                request.EmailNotificationsEnabled,
                request.PushNotificationsEnabled);
            await repository.AddAsync(preference, ct);
        }
        else
        {
            preference.Update(
                request.TransactionNotifications,
                request.SecurityNotifications,
                request.CardNotifications,
                request.LimitNotifications,
                request.ChatNotifications,
                request.EmailNotificationsEnabled,
                request.PushNotificationsEnabled);
            await repository.UpdateAsync(preference, ct);
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
