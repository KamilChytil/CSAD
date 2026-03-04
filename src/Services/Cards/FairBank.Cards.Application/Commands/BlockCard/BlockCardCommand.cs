using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.BlockCard;

public sealed record BlockCardCommand(Guid CardId) : IRequest<CardResponse>;
