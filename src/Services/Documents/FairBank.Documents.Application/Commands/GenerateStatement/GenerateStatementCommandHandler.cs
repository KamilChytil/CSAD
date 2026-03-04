using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Ports;
using FairBank.Documents.Application.Enums;
using MediatR;

namespace FairBank.Documents.Application.Commands.GenerateStatement;

public sealed class GenerateStatementCommandHandler : IRequestHandler<GenerateStatementCommand, StatementResponse>
{
    private readonly IAccountsServiceClient _accountsClient;
    private readonly IStatementGenerator _generator;

    public GenerateStatementCommandHandler(
        IAccountsServiceClient accountsClient,
        IStatementGenerator generator)
    {
        _accountsClient = accountsClient;
        _generator = generator;
    }

    public async Task<StatementResponse> Handle(GenerateStatementCommand request, CancellationToken ct)
    {
        var txs = await _accountsClient.GetTransactionsAsync(request.AccountId, request.From, request.To, ct);
        // generator will create proper file and content type
        return await _generator.GenerateAsync(request.AccountId, request.From, request.To, txs, request.Format);
    }
}
