using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Cards.Application.Commands.SetPin;

public sealed class SetPinCommandHandler(
    ICardRepository cardRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetPinCommand, Unit>
{
    public async Task<Unit> Handle(SetPinCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        var pinHash = FairBank.SharedKernel.Security.PasswordHasher.Hash(request.Pin);
        card.SetPin(pinHash);

        await cardRepository.UpdateAsync(card, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
