using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Commands.RejectApplication;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;

namespace FairBank.Products.UnitTests.Application.Commands;

public class RejectApplicationCommandHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RejectApplicationCommandHandler _handler;

    public RejectApplicationCommandHandlerTests()
    {
        _handler = new RejectApplicationCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenApplicationExists_ShouldRejectAndReturnResponse()
    {
        // Arrange
        var application = ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "{}", 5000m);
        var reviewerId = Guid.NewGuid();
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new RejectApplicationCommand(application.Id, reviewerId, "Insufficient income");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Rejected");
        result.ReviewedBy.Should().Be(reviewerId);
        result.Note.Should().Be("Insufficient income");
        result.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateAsyncAndSaveChanges()
    {
        // Arrange
        var application = ProductApplication.Create(Guid.NewGuid(), ProductType.Mortgage, "{}", 10000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new RejectApplicationCommand(application.Id, Guid.NewGuid(), "Too risky");

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
        var command = new RejectApplicationCommand(Guid.NewGuid(), Guid.NewGuid(), "No reason");
        _repository.GetByIdAsync(command.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ProductApplication?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
