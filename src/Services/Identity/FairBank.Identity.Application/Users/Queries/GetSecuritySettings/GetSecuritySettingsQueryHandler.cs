using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetSecuritySettings;

public sealed class GetSecuritySettingsQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsResponse?>
{
    public async Task<SecuritySettingsResponse?> Handle(GetSecuritySettingsQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);

        if (user is null) return null;

        return new SecuritySettingsResponse(
            user.AllowInternationalPayments,
            user.NightTransactionsEnabled,
            user.RequireApprovalAbove);
    }
}
