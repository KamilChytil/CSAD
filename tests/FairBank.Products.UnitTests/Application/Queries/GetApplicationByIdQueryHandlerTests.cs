using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Queries.GetApplicationById;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;

namespace FairBank.Products.UnitTests.Application.Queries;

public class GetApplicationByIdQueryHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly GetApplicationByIdQueryHandler _handler;

    public GetApplicationByIdQueryHandlerTests()
    {
        _handler = new GetApplicationByIdQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenApplicationExists_ShouldReturnMappedResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var application = ProductApplication.Create(userId, ProductType.PersonalLoan, "{\"amount\":200000}", 5000m);
        _repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        var query = new GetApplicationByIdQuery(application.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(application.Id);
        result.UserId.Should().Be(userId);
        result.ProductType.Should().Be("PersonalLoan");
        result.Status.Should().Be("Pending");
        result.Parameters.Should().Be("{\"amount\":200000}");
        result.MonthlyPayment.Should().Be(5000m);
    }

    [Fact]
    public async Task Handle_WhenApplicationNotFound_ShouldReturnNull()
    {
        // Arrange
        var query = new GetApplicationByIdQuery(Guid.NewGuid());
        _repository.GetByIdAsync(query.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ProductApplication?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
