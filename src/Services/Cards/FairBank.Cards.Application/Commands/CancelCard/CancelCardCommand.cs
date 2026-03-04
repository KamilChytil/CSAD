using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.CancelCard;

public sealed record CancelCardCommand(Guid CardId) : IRequest<CardResponse>;
