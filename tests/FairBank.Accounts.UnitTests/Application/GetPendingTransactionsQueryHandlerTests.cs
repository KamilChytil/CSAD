using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Application.Queries.GetPendingTransactions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Application;

public class GetPendingTransactionsQueryHandlerTests
{
    private readonly IPendingTransactionStore _store = Substitute.For<IPendingTransactionStore>();

    [Fact]
    public async Task Handle_WithPendingTransactions_ShouldReturnList()
    {
        var accountId = Guid.NewGuid();
        var tx1 = PendingTransaction.Create(accountId, Money.Create(100, Currency.CZK), "Nákup 1", Guid.NewGuid());
        var tx2 = PendingTransaction.Create(accountId, Money.Create(200, Currency.CZK), "Nákup 2", Guid.NewGuid());

        _store.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PendingTransaction> { tx1, tx2 });

        var handler = new GetPendingTransactionsQueryHandler(_store);
        var query = new GetPendingTransactionsQuery(accountId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Amount.Should().Be(100);
        result[1].Amount.Should().Be(200);
    }

    [Fact]
    public async Task Handle_WithNoTransactions_ShouldReturnEmptyList()
    {
        var accountId = Guid.NewGuid();
        _store.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PendingTransaction>());

        var handler = new GetPendingTransactionsQueryHandler(_store);
        var query = new GetPendingTransactionsQuery(accountId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
