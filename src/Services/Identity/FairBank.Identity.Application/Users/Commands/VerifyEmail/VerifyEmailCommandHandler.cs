using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.VerifyEmail;

public sealed class VerifyEmailCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<VerifyEmailCommand, bool>
{
    public async Task<bool> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailVerificationTokenAsync(request.Token, ct)
            ?? throw new InvalidOperationException("Invalid verification token.");

        user.VerifyEmail(request.Token);
        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "VerifyEmail",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Successfully verified email"), ct);

        return true;
    }
}
