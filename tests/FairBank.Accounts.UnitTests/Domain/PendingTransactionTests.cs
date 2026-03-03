using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class PendingTransactionTests
{
    [Fact]
    public void Create_ShouldInitializeWithPendingStatus()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test withdrawal",
            Guid.NewGuid());

        tx.Status.Should().Be(PendingTransactionStatus.Pending);
        tx.Amount.Amount.Should().Be(100);
        tx.GetUncommittedEvents().Should().HaveCount(1);
    }

    [Fact]
    public void Approve_ShouldChangeStatusToApproved()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.ClearUncommittedEvents();

        var approverId = Guid.NewGuid();
        tx.Approve(approverId);

        tx.Status.Should().Be(PendingTransactionStatus.Approved);
        tx.ApproverId.Should().Be(approverId);
        tx.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_ShouldChangeStatusToRejected()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.ClearUncommittedEvents();

        var approverId = Guid.NewGuid();
        tx.Reject(approverId, "Too expensive");

        tx.Status.Should().Be(PendingTransactionStatus.Rejected);
        tx.RejectionReason.Should().Be("Too expensive");
    }

    [Fact]
    public void Approve_AlreadyApproved_ShouldThrow()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.Approve(Guid.NewGuid());

        var act = () => tx.Approve(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
