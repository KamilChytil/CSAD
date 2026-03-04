using FluentValidation;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z\d]").WithMessage("Password must contain a special character.");

        RuleFor(x => x.PersonalIdNumber)
            .MinimumLength(9).When(x => !string.IsNullOrEmpty(x.PersonalIdNumber))
            .WithMessage("Personal ID number must be at least 9 characters.");

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob == null || dob.Value.AddYears(15) <= DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("User must be at least 15 years old.");

        RuleFor(x => x.Phone)
            .MinimumLength(9).When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Phone number must be at least 9 digits.");

        RuleFor(x => x.ZipCode)
            .MinimumLength(5).When(x => !string.IsNullOrEmpty(x.ZipCode))
            .WithMessage("ZIP code must be at least 5 characters.");
    }
}
