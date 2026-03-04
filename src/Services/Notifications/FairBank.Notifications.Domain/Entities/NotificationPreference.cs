namespace FairBank.Notifications.Domain.Entities;

public sealed class NotificationPreference
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public bool TransactionNotifications { get; private set; } = true;
    public bool SecurityNotifications { get; private set; } = true;
    public bool CardNotifications { get; private set; } = true;
    public bool LimitNotifications { get; private set; } = true;
    public bool ChatNotifications { get; private set; } = true;
    public bool EmailNotificationsEnabled { get; private set; } = true;
    public bool PushNotificationsEnabled { get; private set; }

    private NotificationPreference() { }

    public static NotificationPreference CreateDefault(Guid userId)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };
    }

    public void Update(bool transaction, bool security, bool card, bool limit, bool chat, bool email, bool push)
    {
        TransactionNotifications = transaction;
        SecurityNotifications = true; // Security can never be disabled
        CardNotifications = card;
        LimitNotifications = limit;
        ChatNotifications = chat;
        EmailNotificationsEnabled = email;
        PushNotificationsEnabled = push;
    }
}
