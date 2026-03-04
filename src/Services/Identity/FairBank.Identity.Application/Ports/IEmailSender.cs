namespace FairBank.Identity.Application.Ports;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string to, string verificationToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string to, string resetToken, CancellationToken ct = default);
    Task SendSecurityAlertAsync(string to, string subject, string message, CancellationToken ct = default);
}
