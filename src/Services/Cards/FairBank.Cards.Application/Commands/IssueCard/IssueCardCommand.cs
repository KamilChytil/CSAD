using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Enums;
using MediatR;

namespace FairBank.Cards.Application.Commands.IssueCard;

public sealed record IssueCardCommand(
    Guid AccountId,
    Guid UserId,
    string CardholderName,
    CardType CardType,
    CardBrand CardBrand) : IRequest<CardResponse>;
