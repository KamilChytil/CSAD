using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.StandingOrders.Commands.CancelStandingOrder;

public sealed record CancelStandingOrderCommand(Guid StandingOrderId) : IRequest<bool>;

public sealed class CancelStandingOrderCommandHandler(
    IStandingOrderRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelStandingOrderCommand, bool>
{
    public async Task<bool> Handle(CancelStandingOrderCommand request, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(request.StandingOrderId, ct)
            ?? throw new InvalidOperationException("Standing order not found.");

        order.Deactivate();
        await repository.UpdateAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
