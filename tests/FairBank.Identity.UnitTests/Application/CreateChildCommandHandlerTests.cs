using FluentAssertions;
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;
using NSubstitute;

namespace FairBank.Identity.UnitTests.Application;

public class CreateChildCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidParent_ShouldCreateChildUser()
    {
        var repo = Substitute.For<IUserRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var sender = Substitute.For<ISender>();
        var parentId = Guid.NewGuid();
        var parent = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);

        repo.GetByIdAsync(parentId, Arg.Any<CancellationToken>()).Returns(parent);
        repo.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateChildCommandHandler(repo, uow, sender);
        var command = new CreateChildCommand(parentId, "Petr", "Novák", "petr@example.com", "Password1!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.FirstName.Should().Be("Petr");
        result.Role.Should().Be(UserRole.Child);
        await repo.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonClientParent_ShouldThrow()
    {
        var repo = Substitute.For<IUserRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var sender = Substitute.For<ISender>();
        var parentId = Guid.NewGuid();
        var banker = User.Create("Bankéř", "Test", Email.Create("banker@example.com"), "hash", UserRole.Banker);

        repo.GetByIdAsync(parentId, Arg.Any<CancellationToken>()).Returns(banker);

        var handler = new CreateChildCommandHandler(repo, uow, sender);
        var command = new CreateChildCommand(parentId, "Dítě", "Test", "child@example.com", "Password1!");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only clients can create child accounts.");
    }
}
