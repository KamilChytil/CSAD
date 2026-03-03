using FairBank.Payments.Application.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Templates.Commands.CreateTemplate;

public sealed record CreateTemplateCommand(
    Guid OwnerAccountId,
    string Name,
    string RecipientAccountNumber,
    string Currency,
    string? RecipientName = null,
    decimal? DefaultAmount = null,
    string? DefaultDescription = null) : IRequest<PaymentTemplateResponse>;
