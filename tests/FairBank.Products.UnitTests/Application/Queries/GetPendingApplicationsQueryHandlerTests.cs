using FluentAssertions;
using NSubstitute;
using FairBank.Products.Application.Queries.GetPendingApplications;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;

namespace FairBank.Products.UnitTests.Application.Queries;

public class GetPendingApplicationsQueryHandlerTests
{
    private readonly IProductApplicationRepository _repository = Substitute.For<IProductApplicationRepository>();
    private readonly GetPendingApplicationsQueryHandler _handler;

    public GetPendingApplicationsQueryHandlerTests()
    {
        _handler = new GetPendingApplicationsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllPendingApplications()
    {
        // Arrange
        var apps = new List<ProductApplication>
        {
            ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "{}", 5000m),
            ProductApplication.Create(Guid.NewGuid(), ProductType.TravelInsurance, "{}", 200m),
            ProductApplication.Create(Guid.NewGuid(), ProductType.Mortgage, "{}", 12000m)
        };
        _repository.GetPendingAsync(Arg.Any<CancellationToken>())
            .Returns(apps.AsReadOnly());

        var query = new GetPendingApplicationsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.Status.Should().Be("Pending"));
    }

    [Fact]
    public async Task Handle_WhenNoPendingApplications_ShouldReturnEmptyList()
    {
        // Arrange
        _repository.GetPendingAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProductApplication>().AsReadOnly());

        var query = new GetPendingApplicationsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
