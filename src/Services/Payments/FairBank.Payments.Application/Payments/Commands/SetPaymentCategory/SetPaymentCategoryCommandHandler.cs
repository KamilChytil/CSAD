using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Payments.Commands.SetPaymentCategory;

public sealed class SetPaymentCategoryCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetPaymentCategoryCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(SetPaymentCategoryCommand request, CancellationToken ct)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, ct)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.SetCategory(request.Category);

        await paymentRepository.UpdateAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new PaymentResponse(
            payment.Id, payment.SenderAccountId, payment.RecipientAccountId,
            payment.SenderAccountNumber, payment.RecipientAccountNumber,
            payment.Amount, payment.Currency.ToString(), payment.Description,
            payment.Type.ToString(), payment.Status.ToString(),
            payment.Category.ToString(),
            payment.CreatedAt, payment.CompletedAt, payment.FailureReason);
    }
}
