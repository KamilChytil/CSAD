using FluentAssertions;
using NSubstitute;
using FairBank.Documents.Application.Commands.GenerateStatement;
using FairBank.Documents.Application.Enums;
using FairBank.Documents.Application.Ports;
using FairBank.Documents.Application.DTOs;

namespace FairBank.Documents.UnitTests.Application;

public class GenerateStatementCommandHandlerTests
{
    private readonly IAccountsServiceClient _accounts = Substitute.For<IAccountsServiceClient>();
    private readonly IStatementGenerator _generator = Substitute.For<IStatementGenerator>();

    [Fact]
    public async Task Handle_ShouldInvokeGeneratorWithTransactions()
    {
        var accountId = Guid.NewGuid();
        var txs = new List<DocumentTransactionDto>
        {
            new(DateTime.UtcNow, "Deposit", 100, "CZK", "Salary")
        };
        _accounts.GetTransactionsAsync(accountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(txs);
        _generator.GenerateAsync(accountId, null, null, txs, StatementFormat.Pdf)
            .Returns(new StatementResponse(Array.Empty<byte>(), "application/pdf", "file.pdf"));

        var handler = new GenerateStatementCommandHandler(_accounts, _generator);
        var command = new GenerateStatementCommand(accountId, null, null, StatementFormat.Pdf);

        var result = await handler.Handle(command, CancellationToken.None);
        result.ContentType.Should().Be("application/pdf");
        await _generator.Received(1).GenerateAsync(accountId, null, null, txs, StatementFormat.Pdf);
    }
}