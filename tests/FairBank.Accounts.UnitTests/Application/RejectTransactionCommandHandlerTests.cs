using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.RejectTransaction;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Application;

public class RejectTransactionCommandHandlerTests
{
    private readonly IPendingTransactionStore _pendingStore = Substitute.For<IPendingTransactionStore>();
    private readonly INotificationClient _notificationClient = Substitute.For<INotificationClient>();

    [Fact]
    public async Task Handle_WithValidTransaction_ShouldReject()
    {
        var tx = PendingTransaction.Create(Guid.NewGuid(), Money.Create(300, Currency.CZK), "Drahý nákup", Guid.NewGuid());
        var txId = tx.Id;

        _pendingStore.LoadAsync(txId, Arg.Any<CancellationToken>()).Returns(tx);

        var handler = new RejectTransactionCommandHandler(_pendingStore, _notificationClient);
        var approverId = Guid.NewGuid();
        var command = new RejectTransactionCommand(txId, approverId, "Příliš drahé");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(PendingTransactionStatus.Rejected);
        result.Description.Should().Be("Drahý nákup");
        await _pendingStore.Received(1).AppendEventsAsync(tx, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ShouldThrow()
    {
        _pendingStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PendingTransaction?)null);

        var handler = new RejectTransactionCommandHandler(_pendingStore, _notificationClient);
        var command = new RejectTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Pending transaction not found.");
    }
}
