using FluentAssertions;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Domain.Enums;

namespace FairBank.Identity.UnitTests.Application;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyFirstName_ShouldFail()
    {
        var command = new RegisterUserCommand("", "Novák", "jan@example.com", "Password1!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_WithInvalidEmail_ShouldFail()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "not-an-email", "Password1!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_WithShortPassword_ShouldFail()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Aa1!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Validate_WithNoUppercasePassword_ShouldFail()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "password1!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNoDigitPassword_ShouldFail()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password!!", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNoSpecialCharPassword_ShouldFail()
    {
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password11", UserRole.Client);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
