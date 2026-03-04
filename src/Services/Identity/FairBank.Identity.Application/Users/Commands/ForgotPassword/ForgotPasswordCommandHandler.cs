using FairBank.Identity.Application.Ports;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        // Silently succeed if email is invalid or user not found (no user enumeration)
        Email email;
        try
        {
            email = Email.Create(request.Email);
        }
        catch (ArgumentException)
        {
            return;
        }

        var user = await userRepository.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
            return;

        var resetToken = user.GeneratePasswordResetToken();
        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await emailSender.SendPasswordResetAsync(user.Email.Value, resetToken, ct);

        await sender.Send(new RecordAuditLogCommand(
            "ForgotPassword",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            $"Requested password reset token to be sent to {email.Value}"), ct);
    }
}
