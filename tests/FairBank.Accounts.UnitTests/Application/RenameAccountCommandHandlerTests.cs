using FairBank.Accounts.Application.Commands.RenameAccount;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace FairBank.Accounts.UnitTests.Application;

public class RenameAccountCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidAlias_ShouldRenameAccount()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new RenameAccountCommandHandler(_eventStore);
        var result = await handler.Handle(
            new RenameAccountCommand(account.Id, "Sporeni na dovolenou"),
            CancellationToken.None);

        result.Should().NotBeNull();
        await _eventStore.Received(1).AppendEventsAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }
}
