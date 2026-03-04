namespace FairBank.Identity.Application.Users.DTOs;

public sealed record SecuritySettingsResponse(
    bool AllowInternationalPayments,
    bool NightTransactionsEnabled,
    decimal? RequireApprovalAbove);
