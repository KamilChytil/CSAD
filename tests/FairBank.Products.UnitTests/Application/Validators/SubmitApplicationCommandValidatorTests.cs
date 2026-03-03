using FluentAssertions;
using FluentValidation.TestHelper;
using FairBank.Products.Application.Commands.SubmitApplication;

namespace FairBank.Products.UnitTests.Application.Validators;

public class SubmitApplicationCommandValidatorTests
{
    private readonly SubmitApplicationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "PersonalLoan", "{\"amount\":200000}", 5000m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        var command = new SubmitApplicationCommand(
            Guid.Empty, "PersonalLoan", "{}", 1000m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_WithEmptyProductType_ShouldFail()
    {
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "", "{}", 1000m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ProductType);
    }

    [Fact]
    public void Validate_WithEmptyParameters_ShouldFail()
    {
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "PersonalLoan", "", 1000m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Parameters);
    }

    [Fact]
    public void Validate_WithNegativeMonthlyPayment_ShouldFail()
    {
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "PersonalLoan", "{}", -1m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MonthlyPayment);
    }

    [Fact]
    public void Validate_WithZeroMonthlyPayment_ShouldPass()
    {
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "PersonalLoan", "{}", 0m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.MonthlyPayment);
    }
}
