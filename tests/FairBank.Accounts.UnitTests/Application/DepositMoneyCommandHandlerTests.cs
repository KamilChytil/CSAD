using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class DepositMoneyCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidDeposit_ShouldIncreaseBalance()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new DepositMoneyCommandHandler(_eventStore);
        var command = new DepositMoneyCommand(account.Id, 500m, Currency.CZK, "Salary");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Balance.Should().Be(500m);
        await _eventStore.Received(1).AppendEventsAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ShouldThrow()
    {
        // Arrange
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var handler = new DepositMoneyCommandHandler(_eventStore);
        var command = new DepositMoneyCommand(Guid.NewGuid(), 500m, Currency.CZK, "Test");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
