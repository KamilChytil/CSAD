using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.ApproveTransaction;

public sealed record ApproveTransactionCommand(
    Guid TransactionId,
    Guid ApproverId) : IRequest<PendingTransactionResponse>;
