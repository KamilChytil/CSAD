using FluentValidation;

namespace FairBank.Payments.Application.Payments.Commands.SendPayment;

public sealed class SendPaymentCommandValidator : AbstractValidator<SendPaymentCommand>
{
    public SendPaymentCommandValidator()
    {
        RuleFor(x => x.SenderAccountId).NotEmpty();
        RuleFor(x => x.RecipientAccountNumber).NotEmpty().MinimumLength(5);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.Currency).NotEmpty();
    }
}
