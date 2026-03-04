using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Enums;
using MediatR;

namespace FairBank.Payments.Application.Payments.Commands.SetPaymentCategory;

public sealed record SetPaymentCategoryCommand(
    Guid PaymentId,
    PaymentCategory Category) : IRequest<PaymentResponse>;
