using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.ChangeEmail;

public sealed class ChangeEmailCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ChangeEmailCommand>
{
    public async Task Handle(ChangeEmailCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var newEmail = Email.Create(request.NewEmail);

        if (await userRepository.ExistsWithEmailAsync(newEmail, ct))
            throw new InvalidOperationException($"User with email '{request.NewEmail}' already exists.");

        user.ChangeEmail(newEmail);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "ChangeEmail",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            $"Changed email to {newEmail.Value}"), ct);
    }
}
