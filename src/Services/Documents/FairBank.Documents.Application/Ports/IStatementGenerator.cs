using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;

namespace FairBank.Documents.Application.Ports;

public interface IStatementGenerator
{
    Task<StatementResponse> GenerateAsync(
        Guid accountId,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<DocumentTransactionDto> transactions,
        StatementFormat format);
}
