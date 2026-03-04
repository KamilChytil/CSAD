using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.Deactivate();

        await userRepository.UpdateAsync(user, ct);

        await sender.Send(new RecordAuditLogCommand(
            Action: "UserDeactivated",
            EntityName: "User",
            EntityId: user.Id.ToString(),
            Details: user.Email.Value
        ), ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
