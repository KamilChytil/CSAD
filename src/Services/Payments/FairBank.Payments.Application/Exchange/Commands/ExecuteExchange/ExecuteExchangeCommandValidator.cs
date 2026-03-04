using FluentValidation;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed class ExecuteExchangeCommandValidator : AbstractValidator<ExecuteExchangeCommand>
{
    public ExecuteExchangeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.TargetAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.FromCurrency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.ToCurrency).NotEmpty().MaximumLength(10);
        RuleFor(x => x).Must(x => !string.Equals(x.FromCurrency, x.ToCurrency, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Cannot exchange same currency.");
    }
}
