using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.ActivateUser;
using FairBank.Identity.Application.Users.Commands.DeactivateUser;
using FairBank.Identity.Application.Users.Commands.DeleteUser;
using FairBank.Identity.Application.Users.Commands.UpdateUserRole;
using FairBank.Identity.Application.Users.Queries.GetAllUsers;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;

namespace FairBank.Identity.UnitTests.Application;

public class AdminCommandsTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private User CreateUser(string email = "jan@example.com", UserRole role = UserRole.Client)
    {
        return User.Create("Jan", "Novak", Email.Create(email),
            BCrypt.Net.BCrypt.HashPassword("Password1!"), role);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnPaginatedResults()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser("user1@example.com"),
            CreateUser("user2@example.com"),
            CreateUser("user3@example.com")
        };

        _userRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(users);

        var handler = new GetAllUsersQueryHandler(_userRepository);
        var query = new GetAllUsersQuery(Page: 1, PageSize: 2);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task UpdateUserRole_ShouldChangeRole()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new UpdateUserRoleCommandHandler(_userRepository, _unitOfWork);
        var command = new UpdateUserRoleCommand(userId, UserRole.Banker);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.Role.Should().Be(UserRole.Banker);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUser_ShouldSetInactive()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        user.IsActive.Should().BeTrue(); // verify initial state

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new DeactivateUserCommandHandler(_userRepository, _unitOfWork);
        var command = new DeactivateUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.IsActive.Should().BeFalse();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUser_ShouldSetActive()
    {
        // Arrange
        var user = CreateUser();
        user.Deactivate(); // set inactive first
        user.IsActive.Should().BeFalse();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ActivateUserCommandHandler(_userRepository, _unitOfWork);
        var command = new ActivateUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.IsActive.Should().BeTrue();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUser_ShouldSoftDelete()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new DeleteUserCommandHandler(_userRepository, _unitOfWork, _auditLogger);
        var command = new DeleteUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
