using FluentValidation;

namespace FairBank.Payments.Application.StandingOrders.Commands.CreateStandingOrder;

public sealed class CreateStandingOrderCommandValidator : AbstractValidator<CreateStandingOrderCommand>
{
    public CreateStandingOrderCommandValidator()
    {
        RuleFor(x => x.SenderAccountId).NotEmpty();
        RuleFor(x => x.RecipientAccountNumber).NotEmpty().MinimumLength(5);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Interval).NotEmpty();
        RuleFor(x => x.FirstExecutionDate).GreaterThanOrEqualTo(DateTime.Today);
    }
}
