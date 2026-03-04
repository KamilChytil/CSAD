using FairBank.Accounts.Application.Commands.CloseAccount;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace FairBank.Accounts.UnitTests.Application;

public class CloseAccountCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithZeroBalance_ShouldCloseAccount()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new CloseAccountCommandHandler(_eventStore);
        var result = await handler.Handle(new CloseAccountCommand(account.Id), CancellationToken.None);

        result.IsActive.Should().BeFalse();
        await _eventStore.Received(1).AppendEventsAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountNotFound_ShouldThrow()
    {
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var handler = new CloseAccountCommandHandler(_eventStore);
        var act = () => handler.Handle(new CloseAccountCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
