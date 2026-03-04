using FairBank.Notifications.Application.DTOs;
using MediatR;

namespace FairBank.Notifications.Application.Commands.UpdatePreferences;

public sealed record UpdatePreferencesCommand(
    Guid UserId,
    bool TransactionNotifications,
    bool SecurityNotifications,
    bool CardNotifications,
    bool LimitNotifications,
    bool ChatNotifications,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled) : IRequest<NotificationPreferenceResponse>;
