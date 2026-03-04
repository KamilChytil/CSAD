namespace FairBank.Web.Shared.Models;

public class NotificationPreferenceDto
{
    public bool TransactionNotifications { get; set; } = true;
    public bool SecurityNotifications { get; set; } = true;
    public bool CardNotifications { get; set; } = true;
    public bool LimitNotifications { get; set; } = true;
    public bool ChatNotifications { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
}
