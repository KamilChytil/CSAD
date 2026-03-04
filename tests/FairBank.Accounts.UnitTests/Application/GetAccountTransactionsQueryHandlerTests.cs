using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Application.Queries.GetAccountTransactions;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Application;

public class GetAccountTransactionsQueryHandlerTests
{
    private readonly IAccountEventStore _eventStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithDepositAndWithdrawal_ShouldReturnTransactionsChronologically()
    {
        var accountId = Guid.NewGuid();
        var deposit = new MoneyDeposited(accountId, 100m, "CZK", "Salary", DateTime.UtcNow.AddDays(-1));
        var withdraw = new MoneyWithdrawn(accountId, 50m, "CZK", "Groceries", DateTime.UtcNow);

        _eventStore.GetStreamEventsAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<object> { deposit, withdraw });

        var handler = new GetAccountTransactionsQueryHandler(_eventStore);
        var query = new GetAccountTransactionsQuery(accountId, null, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Type.Should().Be("Deposit");
        result[1].Type.Should().Be("Withdrawal");
    }

    [Fact]
    public async Task Handle_WithDateFilter_ShouldExcludeOutsideRange()
    {
        var accountId = Guid.NewGuid();
        var old = new MoneyDeposited(accountId, 10m, "CZK", "Old", DateTime.UtcNow.AddMonths(-2));
        var recent = new MoneyDeposited(accountId, 20m, "CZK", "Recent", DateTime.UtcNow);

        _eventStore.GetStreamEventsAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<object> { old, recent });

        var handler = new GetAccountTransactionsQueryHandler(_eventStore);
        var from = DateTime.UtcNow.AddMonths(-1);
        var query = new GetAccountTransactionsQuery(accountId, from, null);

        var result = await handler.Handle(query, CancellationToken.None);
        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Recent");
    }
}
