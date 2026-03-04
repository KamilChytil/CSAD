namespace FairBank.Identity.Application.Users.DTOs;

public sealed record TwoFactorSetupResponse(
    string Secret,
    string OtpAuthUri,
    bool IsAlreadyEnabled);
