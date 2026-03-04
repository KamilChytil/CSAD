using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.SetAccountLimits;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class SetAccountLimitsCommandHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidAccount_ShouldSetAccountLimits()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new SetAccountLimitsCommandHandler(_eventStore);
        var command = new SetAccountLimitsCommand(
            account.Id, 50000, 200000, 25000, 30, 15000);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(account.Id);
        account.Limits.Should().NotBeNull();
        account.Limits!.DailyTransactionLimit.Should().Be(50000);
        account.Limits.MonthlyTransactionLimit.Should().Be(200000);
        account.Limits.SingleTransactionLimit.Should().Be(25000);
        account.Limits.DailyTransactionCount.Should().Be(30);
        account.Limits.OnlinePaymentLimit.Should().Be(15000);
        await _eventStore.Received(1).AppendEventsAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccountNotFound_ShouldThrow()
    {
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var handler = new SetAccountLimitsCommandHandler(_eventStore);
        var command = new SetAccountLimitsCommand(
            Guid.NewGuid(), 50000, 200000, 25000, 30, 15000);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account not found.");
    }

    [Fact]
    public async Task Handle_WithInvalidLimits_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new SetAccountLimitsCommandHandler(_eventStore);
        // Daily limit exceeds monthly limit
        var command = new SetAccountLimitsCommand(
            account.Id, 600000, 500000, 25000, 30, 15000);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Daily limit cannot exceed monthly limit*");
    }

    [Fact]
    public async Task Handle_WithNegativeValues_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new SetAccountLimitsCommandHandler(_eventStore);
        var command = new SetAccountLimitsCommand(
            account.Id, -1, 200000, 25000, 30, 15000);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*non-negative*");
    }

    [Fact]
    public async Task Handle_InactiveAccount_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deactivate();
        _eventStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new SetAccountLimitsCommandHandler(_eventStore);
        var command = new SetAccountLimitsCommand(
            account.Id, 50000, 200000, 25000, 30, 15000);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }
}
