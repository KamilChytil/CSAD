using FluentAssertions;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;

namespace FairBank.Identity.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void Create_ShouldInitializeWithUnreadStatus()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(
            userId, NotificationType.TransactionPending, "Nová platba dítěte", "Jan zaplatil 200 CZK");

        notification.Id.Should().NotBe(Guid.Empty);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(NotificationType.TransactionPending);
        notification.Title.Should().Be("Nová platba dítěte");
        notification.Message.Should().Be("Jan zaplatil 200 CZK");
        notification.IsRead.Should().BeFalse();
        notification.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsRead_ShouldSetIsReadTrue()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), NotificationType.TransactionCompleted, "Test", "Test message");
        notification.MarkAsRead();
        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void Create_WithRelatedEntity_ShouldSetOptionalFields()
    {
        var relatedId = Guid.NewGuid();
        var notification = Notification.Create(
            Guid.NewGuid(), NotificationType.TransactionApproved, "Schváleno", "Platba schválena",
            relatedId, "PendingTransaction");

        notification.RelatedEntityId.Should().Be(relatedId);
        notification.RelatedEntityType.Should().Be("PendingTransaction");
    }
}
