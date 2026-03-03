using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Queries.GetChildren;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Application;

public class GetChildrenQueryHandlerTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_WithChildren_ShouldReturnList()
    {
        var parentId = Guid.NewGuid();
        var child1 = User.CreateChild("Petr", "Novák", Email.Create("petr@example.com"), "hash", parentId);
        var child2 = User.CreateChild("Jana", "Nováková", Email.Create("jana@example.com"), "hash", parentId);

        _repo.GetChildrenAsync(parentId, Arg.Any<CancellationToken>())
            .Returns(new List<User> { child1, child2 });

        var handler = new GetChildrenQueryHandler(_repo);
        var query = new GetChildrenQuery(parentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].FirstName.Should().Be("Petr");
        result[0].Role.Should().Be(UserRole.Child);
        result[1].FirstName.Should().Be("Jana");
    }

    [Fact]
    public async Task Handle_WithNoChildren_ShouldReturnEmptyList()
    {
        var parentId = Guid.NewGuid();
        _repo.GetChildrenAsync(parentId, Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var handler = new GetChildrenQueryHandler(_repo);
        var query = new GetChildrenQuery(parentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
