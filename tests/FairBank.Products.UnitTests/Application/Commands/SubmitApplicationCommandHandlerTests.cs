using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Commands.SubmitApplication;
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;

namespace FairBank.Products.UnitTests.Application.Commands;

public class SubmitApplicationCommandHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SubmitApplicationCommandHandler _handler;

    public SubmitApplicationCommandHandlerTests()
    {
        _handler = new SubmitApplicationCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnMappedResponse()
    {
        // Arrange
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "PersonalLoan", "{\"amount\":200000}", 5000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(command.UserId);
        result.ProductType.Should().Be("PersonalLoan");
        result.Status.Should().Be("Pending");
        result.Parameters.Should().Be(command.Parameters);
        result.MonthlyPayment.Should().Be(5000m);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallAddAsyncAndSaveChanges()
    {
        // Arrange
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "Mortgage", "{}", 10000m);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repository.Received(1).AddAsync(Arg.Any<ProductApplication>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidProductType_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "InvalidType", "{}", 1000m);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid product type*InvalidType*");
    }

    [Theory]
    [InlineData("PersonalLoan", ProductType.PersonalLoan)]
    [InlineData("Mortgage", ProductType.Mortgage)]
    [InlineData("TravelInsurance", ProductType.TravelInsurance)]
    [InlineData("PropertyInsurance", ProductType.PropertyInsurance)]
    [InlineData("LifeInsurance", ProductType.LifeInsurance)]
    [InlineData("PaymentProtection", ProductType.PaymentProtection)]
    public async Task Handle_ShouldParseAllProductTypes(string input, ProductType expected)
    {
        // Arrange
        var command = new SubmitApplicationCommand(Guid.NewGuid(), input, "{}", 100m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ProductType.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveProductType_ShouldParseCorrectly()
    {
        // Arrange
        var command = new SubmitApplicationCommand(
            Guid.NewGuid(), "personalloan", "{}", 100m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ProductType.Should().Be("PersonalLoan");
    }

    [Fact]
    public async Task Handle_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Arrange - domain validation throws
        var command = new SubmitApplicationCommand(
            Guid.Empty, "PersonalLoan", "{}", 1000m);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
