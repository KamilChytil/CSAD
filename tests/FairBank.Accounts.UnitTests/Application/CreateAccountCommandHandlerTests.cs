using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class CreateAccountCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAccountWithZeroBalance()
    {
        // Arrange
        var handler = new CreateAccountCommandHandler(_eventStore);
        var command = new CreateAccountCommand(Guid.NewGuid(), Currency.CZK);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OwnerId.Should().Be(command.OwnerId);
        result.Balance.Should().Be(0);
        result.Currency.Should().Be(Currency.CZK);
        result.IsActive.Should().BeTrue();
        result.AccountNumber.Should().MatchRegex(@"^\d{6}-\d{10}/8888$");

        await _eventStore.Received(1).StartStreamAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEurCurrency_ShouldCreateEurAccount()
    {
        // Arrange
        var handler = new CreateAccountCommandHandler(_eventStore);
        var command = new CreateAccountCommand(Guid.NewGuid(), Currency.EUR);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Currency.Should().Be(Currency.EUR);
        result.Balance.Should().Be(0);
    }
}
