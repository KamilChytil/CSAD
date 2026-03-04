using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Commands.CancelApplication;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Application;

namespace FairBank.Products.UnitTests.Application.Commands;

public class CancelApplicationCommandHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelApplicationCommandHandler _handler;

    public CancelApplicationCommandHandlerTests()
    {
        _handler = new CancelApplicationCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenApplicantCancels_ShouldCancelAndReturnResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var application = ProductApplication.Create(userId, ProductType.PersonalLoan, "{}", 5000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new CancelApplicationCommand(application.Id, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Cancelled");
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateAsyncAndSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var application = ProductApplication.Create(userId, ProductType.Mortgage, "{}", 10000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new CancelApplicationCommand(application.Id, userId);

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
        var command = new CancelApplicationCommand(Guid.NewGuid(), Guid.NewGuid());
        _repository.GetByIdAsync(command.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ProductApplication?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_WhenDifferentUserCancels_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var application = ProductApplication.Create(ownerId, ProductType.PersonalLoan, "{}", 5000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var command = new CancelApplicationCommand(application.Id, differentUserId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only the applicant*");
    }
}
