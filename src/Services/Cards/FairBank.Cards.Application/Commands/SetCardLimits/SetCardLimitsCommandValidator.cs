using FluentValidation;

namespace FairBank.Cards.Application.Commands.SetCardLimits;

public sealed class SetCardLimitsCommandValidator : AbstractValidator<SetCardLimitsCommand>
{
    public SetCardLimitsCommandValidator()
    {
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.DailyLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlyLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DailyLimit).LessThanOrEqualTo(x => x.MonthlyLimit)
            .WithMessage("Daily limit cannot exceed monthly limit.");
    }
}
