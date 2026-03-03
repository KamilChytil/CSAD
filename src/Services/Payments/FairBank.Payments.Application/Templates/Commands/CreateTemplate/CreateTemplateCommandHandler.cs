using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateCommandHandler(
    IPaymentTemplateRepository templateRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateTemplateCommand, PaymentTemplateResponse>
{
    public async Task<PaymentTemplateResponse> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            throw new ArgumentException($"Invalid currency: {request.Currency}");

        var template = PaymentTemplate.Create(
            ownerAccountId: request.OwnerAccountId,
            name: request.Name,
            recipientAccountNumber: request.RecipientAccountNumber,
            currency: currency,
            recipientName: request.RecipientName,
            defaultAmount: request.DefaultAmount,
            defaultDescription: request.DefaultDescription);

        await templateRepository.AddAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new PaymentTemplateResponse(
            template.Id, template.OwnerAccountId, template.Name,
            template.RecipientAccountNumber, template.RecipientName,
            template.DefaultAmount, template.Currency.ToString(),
            template.DefaultDescription, template.CreatedAt);
    }
}
