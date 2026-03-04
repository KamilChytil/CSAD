using FairBank.Cards.Application.DTOs;
using MediatR;

namespace FairBank.Cards.Application.Commands.SetCardSettings;

public sealed record SetCardSettingsCommand(
    Guid CardId,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled) : IRequest<CardResponse>;
