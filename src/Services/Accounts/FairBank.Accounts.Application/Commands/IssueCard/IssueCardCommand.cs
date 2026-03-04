using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.IssueCard;

public sealed record IssueCardCommand(
    Guid AccountId,
    string HolderName,
    CardType Type = CardType.Debit) : IRequest<CardResponse>;
