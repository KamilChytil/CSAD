using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.SoftDelete();

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "DeleteUser",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Soft deleted user"), ct);
    }
}
