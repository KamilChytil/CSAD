using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RestoreUser;

public sealed class RestoreUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RestoreUserCommand>
{
    public async Task Handle(RestoreUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetDeletedByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("Deleted user not found.");

        user.Restore();

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
