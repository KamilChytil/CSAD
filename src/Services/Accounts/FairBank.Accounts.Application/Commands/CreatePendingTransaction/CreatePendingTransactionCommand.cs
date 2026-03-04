using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreatePendingTransaction;

public sealed record CreatePendingTransactionCommand(
    Guid AccountId, decimal Amount, Currency Currency,
    string Description, Guid RequestedBy) : IRequest<PendingTransactionResponse>;
