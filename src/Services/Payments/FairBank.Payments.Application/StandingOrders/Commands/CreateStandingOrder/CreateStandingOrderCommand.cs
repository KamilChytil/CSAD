using FairBank.Payments.Application.DTOs;
using MediatR;

namespace FairBank.Payments.Application.StandingOrders.Commands.CreateStandingOrder;

public sealed record CreateStandingOrderCommand(
    Guid SenderAccountId,
    string RecipientAccountNumber,
    decimal Amount,
    string Currency,
    string Interval,
    DateTime FirstExecutionDate,
    string? Description = null,
    DateTime? EndDate = null) : IRequest<StandingOrderResponse>;
