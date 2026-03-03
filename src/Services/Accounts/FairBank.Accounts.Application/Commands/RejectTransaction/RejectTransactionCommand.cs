using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RejectTransaction;

public sealed record RejectTransactionCommand(
    Guid TransactionId,
    Guid ApproverId,
    string Reason) : IRequest<PendingTransactionResponse>;
