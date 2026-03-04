using MediatR;

namespace FairBank.Cards.Application.Commands.SetPin;

public sealed record SetPinCommand(Guid CardId, string Pin) : IRequest<Unit>;
