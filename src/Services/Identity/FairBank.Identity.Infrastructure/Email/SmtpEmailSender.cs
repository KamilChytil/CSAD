using System.Net;
using System.Net.Mail;
using FairBank.Identity.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairBank.Identity.Infrastructure.Email;

public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailVerificationAsync(string to, string verificationToken, CancellationToken ct = default)
    {
        var baseUrl = config["App:BaseUrl"] ?? "http://localhost";
        var verifyUrl = $"{baseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

        var body = $"""
            <h2>Ověření emailové adresy – FairBank</h2>
            <p>Děkujeme za registraci. Klikněte na odkaz pro ověření emailu:</p>
            <p><a href="{verifyUrl}">Ověřit email</a></p>
            <p>Pokud jste se neregistrovali, tento email ignorujte.</p>
            <p>Odkaz je platný 24 hodin.</p>
            """;

        await SendAsync(to, "Ověření emailové adresy – FairBank", body, ct);
    }

    public async Task SendPasswordResetAsync(string to, string resetToken, CancellationToken ct = default)
    {
        var baseUrl = config["App:BaseUrl"] ?? "http://localhost";
        var resetUrl = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

        var body = $"""
            <h2>Obnovení hesla – FairBank</h2>
            <p>Obdrželi jsme žádost o obnovení hesla. Klikněte na odkaz:</p>
            <p><a href="{resetUrl}">Obnovit heslo</a></p>
            <p>Pokud jste žádost nepodali, tento email ignorujte.</p>
            <p>Odkaz je platný 1 hodinu.</p>
            """;

        await SendAsync(to, "Obnovení hesla – FairBank", body, ct);
    }

    public async Task SendSecurityAlertAsync(string to, string subject, string message, CancellationToken ct = default)
    {
        var body = $"""
            <h2>Bezpečnostní upozornění – FairBank</h2>
            <p>{message}</p>
            """;

        await SendAsync(to, subject, body, ct);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var smtpHost = config["Smtp:Host"] ?? "localhost";
            var smtpPort = int.Parse(config["Smtp:Port"] ?? "587");
            var smtpUser = config["Smtp:Username"] ?? "";
            var smtpPass = config["Smtp:Password"] ?? "";
            var fromEmail = config["Smtp:FromEmail"] ?? "noreply@fairbank.cz";
            var fromName = config["Smtp:FromName"] ?? "FairBank";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send email to {To}: {Subject}. Continuing without email.", to, subject);
            // Don't throw - email failures shouldn't block the operation
        }
    }
}
