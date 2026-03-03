using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Application;

public class WithdrawMoneyCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithSufficientFunds_ShouldDecreaseBalance()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000m, Currency.CZK), "Initial");
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new WithdrawMoneyCommandHandler(_eventStore);
        var command = new WithdrawMoneyCommand(account.Id, 300m, Currency.CZK, "ATM");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Balance.Should().Be(700m);
        await _eventStore.Received(1).AppendEventsAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInsufficientFunds_ShouldThrow()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(100m, Currency.CZK), "Initial");
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new WithdrawMoneyCommandHandler(_eventStore);
        var command = new WithdrawMoneyCommand(account.Id, 500m, Currency.CZK, "Too much");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ShouldThrow()
    {
        // Arrange
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var handler = new WithdrawMoneyCommandHandler(_eventStore);
        var command = new WithdrawMoneyCommand(Guid.NewGuid(), 100m, Currency.CZK, "Test");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
