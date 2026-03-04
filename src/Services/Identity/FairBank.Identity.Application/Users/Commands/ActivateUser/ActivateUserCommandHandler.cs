using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ActivateUser;

public sealed class ActivateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ActivateUserCommand>
{
    public async Task Handle(ActivateUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.Activate();

        await userRepository.UpdateAsync(user, ct);

        await sender.Send(new RecordAuditLogCommand(
            Action: "UserActivated",
            EntityName: "User",
            EntityId: user.Id.ToString(),
            Details: user.Email.Value
        ), ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
