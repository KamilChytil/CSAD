using FairBank.Payments.Application.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Payments.Commands.SendPayment;

public sealed record SendPaymentCommand(
    Guid SenderAccountId,
    string RecipientAccountNumber,
    decimal Amount,
    string Currency,
    string? Description,
    bool IsInstant = false) : IRequest<PaymentResponse>;
