using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Queries.GetUserApplications;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;

namespace FairBank.Products.UnitTests.Application.Queries;

public class GetUserApplicationsQueryHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly GetUserApplicationsQueryHandler _handler;

    public GetUserApplicationsQueryHandlerTests()
    {
        _handler = new GetUserApplicationsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllApplicationsForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var apps = new List<ProductApplication>
        {
            ProductApplication.Create(userId, ProductType.PersonalLoan, "{}", 5000m),
            ProductApplication.Create(userId, ProductType.Mortgage, "{}", 15000m)
        };
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(apps.AsReadOnly());

        var query = new GetUserApplicationsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].ProductType.Should().Be("PersonalLoan");
        result[1].ProductType.Should().Be("Mortgage");
        result.Should().AllSatisfy(r => r.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task Handle_WhenNoApplications_ShouldReturnEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ProductApplication>().AsReadOnly());

        var query = new GetUserApplicationsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
