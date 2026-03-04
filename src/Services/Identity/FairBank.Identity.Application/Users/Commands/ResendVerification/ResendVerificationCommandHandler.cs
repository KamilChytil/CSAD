using FairBank.Identity.Application.Ports;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.ResendVerification;

public sealed class ResendVerificationCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ResendVerificationCommand>
{
    public async Task Handle(ResendVerificationCommand request, CancellationToken ct)
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

        if (user is null || user.IsEmailVerified)
            return;

        user.GenerateEmailVerificationToken();
        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await emailSender.SendEmailVerificationAsync(
            user.Email.Value,
            user.EmailVerificationToken!,
            ct);

        await sender.Send(new RecordAuditLogCommand(
            "ResendVerification",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            $"Resent email verification to {email.Value}"), ct);
    }
}
