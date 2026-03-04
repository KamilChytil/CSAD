using FairBank.Cards.Domain.Enums;
using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Cards.Application.Commands.DeactivateAllCardsByUser;

public sealed class DeactivateAllCardsByUserCommandHandler(
    ICardRepository cardRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateAllCardsByUserCommand, int>
{
    public async Task<int> Handle(DeactivateAllCardsByUserCommand request, CancellationToken ct)
    {
        var cards = await cardRepository.GetByUserIdAsync(request.UserId, ct);

        var blocked = 0;
        foreach (var card in cards)
        {
            if (card.Status is CardStatus.Active or CardStatus.Blocked)
            {
                if (card.Status != CardStatus.Blocked)
                    card.Block();
                await cardRepository.UpdateAsync(card, ct);
                blocked++;
            }
        }

        if (blocked > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return blocked;
    }
}
