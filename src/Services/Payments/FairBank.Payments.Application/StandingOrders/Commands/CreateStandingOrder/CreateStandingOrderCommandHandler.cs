using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.StandingOrders.Commands.CreateStandingOrder;

public sealed class CreateStandingOrderCommandHandler(
    IStandingOrderRepository standingOrderRepository,
    IAccountsServiceClient accountsClient,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateStandingOrderCommand, StandingOrderResponse>
{
    public async Task<StandingOrderResponse> Handle(CreateStandingOrderCommand request, CancellationToken ct)
    {
        var senderAccount = await accountsClient.GetAccountByIdAsync(request.SenderAccountId, ct)
            ?? throw new InvalidOperationException("Sender account not found.");

        if (!senderAccount.IsActive)
            throw new InvalidOperationException("Sender account is not active.");

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            throw new ArgumentException($"Invalid currency: {request.Currency}");

        if (!Enum.TryParse<RecurrenceInterval>(request.Interval, true, out var interval))
            throw new ArgumentException($"Invalid interval: {request.Interval}");

        var firstExecUtc = DateTime.SpecifyKind(request.FirstExecutionDate, DateTimeKind.Utc);
        var endDateUtc  = request.EndDate.HasValue
            ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var standingOrder = StandingOrder.Create(
            senderAccountId: request.SenderAccountId,
            senderAccountNumber: senderAccount.AccountNumber,
            recipientAccountNumber: request.RecipientAccountNumber,
            amount: request.Amount,
            currency: currency,
            interval: interval,
            firstExecutionDate: firstExecUtc,
            description: request.Description,
            endDate: endDateUtc);

        await standingOrderRepository.AddAsync(standingOrder, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(standingOrder);
    }

    private static StandingOrderResponse MapToResponse(StandingOrder so) => new(
        so.Id, so.SenderAccountId, so.SenderAccountNumber, so.RecipientAccountNumber,
        so.Amount, so.Currency.ToString(), so.Description,
        so.Interval.ToString(), so.NextExecutionDate, so.EndDate,
        so.IsActive, so.CreatedAt, so.LastExecutedAt, so.ExecutionCount);
}
