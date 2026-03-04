using FairBank.Documents.Application.DTOs;

namespace FairBank.Documents.Application.Ports;

public interface IAccountsServiceClient
{
    Task<IReadOnlyList<DocumentTransactionDto>> GetTransactionsAsync(Guid accountId, DateTime? from, DateTime? to, CancellationToken ct = default);
}
