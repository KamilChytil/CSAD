using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.IssueCard;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class IssueCardCommandHandlerTests
{
    private readonly IAccountEventStore _accountEventStore = Substitute.For<IAccountEventStore>();
    private readonly ICardEventStore _cardEventStore = Substitute.For<ICardEventStore>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldIssueCard()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _accountEventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var handler = new IssueCardCommandHandler(_accountEventStore, _cardEventStore);
        var command = new IssueCardCommand(account.Id, "Jan Novak", CardType.Debit);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccountId.Should().Be(account.Id);
        result.HolderName.Should().Be("Jan Novak");
        result.Type.Should().Be(CardType.Debit);
        result.IsActive.Should().BeTrue();
        result.IsFrozen.Should().BeFalse();
        result.OnlinePaymentsEnabled.Should().BeTrue();
        result.ContactlessEnabled.Should().BeTrue();
        result.MaskedNumber.Should().StartWith("**** **** **** ");
        result.DailyLimit.Should().BeNull();
        result.MonthlyLimit.Should().BeNull();

        await _cardEventStore.Received(1).StartStreamAsync(Arg.Any<Card>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _accountEventStore.LoadAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var handler = new IssueCardCommandHandler(_accountEventStore, _cardEventStore);
        var command = new IssueCardCommand(accountId, "Jan Novak");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Account {accountId} not found.");
    }
}
