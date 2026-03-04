using FluentValidation;

namespace FairBank.Cards.Application.Commands.IssueCard;

public sealed class IssueCardCommandValidator : AbstractValidator<IssueCardCommand>
{
    public IssueCardCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CardholderName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.CardType).IsInEnum();
        RuleFor(x => x.CardBrand).IsInEnum();
    }
}
