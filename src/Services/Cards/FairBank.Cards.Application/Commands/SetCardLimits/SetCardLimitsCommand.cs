using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.SetCardLimits;

public sealed record SetCardLimitsCommand(
    Guid CardId,
    decimal DailyLimit,
    decimal MonthlyLimit) : IRequest<CardResponse>;
