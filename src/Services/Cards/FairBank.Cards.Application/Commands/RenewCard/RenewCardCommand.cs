using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.RenewCard;

public sealed record RenewCardCommand(Guid CardId) : IRequest<CardResponse>;
