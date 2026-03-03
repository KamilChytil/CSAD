using FluentValidation;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed class CreateChildCommandValidator : AbstractValidator<CreateChildCommand>
{
    public CreateChildCommandValidator()
    {
        RuleFor(x => x.ParentId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
