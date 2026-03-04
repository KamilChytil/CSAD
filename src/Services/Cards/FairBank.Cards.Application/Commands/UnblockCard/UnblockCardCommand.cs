using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.UnblockCard;

public sealed record UnblockCardCommand(Guid CardId) : IRequest<CardResponse>;
