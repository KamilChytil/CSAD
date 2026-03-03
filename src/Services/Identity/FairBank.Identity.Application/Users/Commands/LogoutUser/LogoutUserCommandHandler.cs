using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LogoutUser;

public sealed class LogoutUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutUserCommand>
{
    public async Task Handle(LogoutUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null) return;

        user.InvalidateSession(request.SessionId);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
