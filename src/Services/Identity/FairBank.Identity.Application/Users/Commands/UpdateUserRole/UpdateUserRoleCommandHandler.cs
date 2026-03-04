using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<UpdateUserRoleCommand>
{
    public async Task Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var oldRole = user.Role;
        user.ChangeRole(request.NewRole);

        await userRepository.UpdateAsync(user, ct);

        await sender.Send(new RecordAuditLogCommand(
            Action: "UserRoleChanged",
            EntityName: "User",
            EntityId: user.Id.ToString(),
            Details: $"{user.Email.Value}: {oldRole} -> {request.NewRole}"
        ), ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
