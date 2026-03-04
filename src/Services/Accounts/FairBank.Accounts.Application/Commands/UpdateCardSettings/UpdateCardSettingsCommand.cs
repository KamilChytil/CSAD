using MediatR;

namespace FairBank.Accounts.Application.Commands.UpdateCardSettings;

public sealed record UpdateCardSettingsCommand(
    Guid CardId,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled) : IRequest;
