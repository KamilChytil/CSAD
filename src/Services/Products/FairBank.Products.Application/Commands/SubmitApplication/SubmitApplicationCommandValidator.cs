using FluentValidation;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ProductType).NotEmpty();
        RuleFor(x => x.Parameters).NotEmpty();
        RuleFor(x => x.MonthlyPayment).GreaterThanOrEqualTo(0);
    }
}
