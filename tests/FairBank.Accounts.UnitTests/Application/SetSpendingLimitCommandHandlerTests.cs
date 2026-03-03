using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.SetSpendingLimit;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class SetSpendingLimitCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidAccount_ShouldSetSpendingLimit()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new SetSpendingLimitCommandHandler(_eventStore);
        var command = new SetSpendingLimitCommand(account.Id, 500, Currency.CZK);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(account.Id);
        account.SpendingLimit!.Amount.Should().Be(500);
        account.RequiresApproval.Should().BeTrue();
        await _eventStore.Received(1).AppendEventsAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountNotFound_ShouldThrow()
    {
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var handler = new SetSpendingLimitCommandHandler(_eventStore);
        var command = new SetSpendingLimitCommand(Guid.NewGuid(), 500, Currency.CZK);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account not found.");
    }
}
