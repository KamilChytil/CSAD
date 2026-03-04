using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Commands.ApproveApplication;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;

namespace FairBank.Products.UnitTests.Application.Commands;

public class ApproveApplicationCommandHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ApproveApplicationCommandHandler _handler;

    public ApproveApplicationCommandHandlerTests()
    {
        _handler = new ApproveApplicationCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenApplicationExists_ShouldApproveAndReturnResponse()
    {
        // Arrange
        var application = ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "{}", 5000m);
        var reviewerId = Guid.NewGuid();
        var command = new ApproveApplicationCommand(application.Id, reviewerId, "Approved");

        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Active");
        result.ReviewedBy.Should().Be(reviewerId);
        result.Note.Should().Be("Approved");
        result.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateAsyncAndSaveChanges()
    {
        // Arrange
        var application = ProductApplication.Create(Guid.NewGuid(), ProductType.Mortgage, "{}", 10000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new ApproveApplicationCommand(application.Id, Guid.NewGuid());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(application, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new ApproveApplicationCommand(Guid.NewGuid(), Guid.NewGuid());
        _repository.GetByIdAsync(command.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ProductApplication?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectlyMappedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var application = ProductApplication.Create(userId, ProductType.TravelInsurance, "{\"dest\":\"EU\"}", 350m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new ApproveApplicationCommand(application.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().Be(application.Id);
        result.UserId.Should().Be(userId);
        result.ProductType.Should().Be("TravelInsurance");
        result.Parameters.Should().Be("{\"dest\":\"EU\"}");
        result.MonthlyPayment.Should().Be(350m);
    }
}
