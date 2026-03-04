namespace FairBank.Notifications.Application.DTOs;

public sealed record NotificationPreferenceResponse(
    Guid Id,
    Guid UserId,
    bool TransactionNotifications,
    bool SecurityNotifications,
    bool CardNotifications,
    bool LimitNotifications,
    bool ChatNotifications,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled);
