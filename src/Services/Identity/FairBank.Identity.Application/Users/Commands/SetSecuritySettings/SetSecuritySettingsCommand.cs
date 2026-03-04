using MediatR;

namespace FairBank.Identity.Application.Users.Commands.SetSecuritySettings;

public sealed record SetSecuritySettingsCommand(
    Guid UserId,
    bool AllowInternationalPayments,
    bool NightTransactionsEnabled,
    decimal? RequireApprovalAbove) : IRequest<bool>;
