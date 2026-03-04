using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Ports;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new RegisterUserCommandHandler(_userRepository, _emailSender, _unitOfWork, _auditLogger, _sender);
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jan");
        result.LastName.Should().Be("Novák");
        result.Email.Should().Be("jan@example.com");
        result.Role.Should().Be(UserRole.Client);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendEmailVerificationAsync(
            "jan@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrow()
    {
        // Arrange
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RegisterUserCommandHandler(_userRepository, _emailSender, _unitOfWork, _auditLogger, _sender);
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}
