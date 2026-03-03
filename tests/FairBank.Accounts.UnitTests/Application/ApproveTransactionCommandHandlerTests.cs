using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.ApproveTransaction;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Application;

public class ApproveTransactionCommandHandlerTests
{
    private readonly IPendingTransactionStore _pendingStore = Substitute.For<IPendingTransactionStore>();
    private readonly IAccountEventStore _accountStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidTransaction_ShouldApproveAndWithdraw()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000, Currency.CZK), "Initial");
        account.ClearUncommittedEvents();

        var tx = PendingTransaction.Create(accountId, Money.Create(200, Currency.CZK), "Nákup", Guid.NewGuid());
        var txId = tx.Id;

        _pendingStore.LoadAsync(txId, Arg.Any<CancellationToken>()).Returns(tx);
        _accountStore.LoadAsync(tx.AccountId, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new ApproveTransactionCommandHandler(_pendingStore, _accountStore);
        var approverId = Guid.NewGuid();
        var command = new ApproveTransactionCommand(txId, approverId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(PendingTransactionStatus.Approved);
        result.Amount.Should().Be(200);
        await _pendingStore.Received(1).AppendEventsAsync(tx, Arg.Any<CancellationToken>());
        await _accountStore.Received(1).AppendEventsAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ShouldThrow()
    {
        _pendingStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PendingTransaction?)null);

        var handler = new ApproveTransactionCommandHandler(_pendingStore, _accountStore);
        var command = new ApproveTransactionCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Pending transaction not found.");
    }
}
