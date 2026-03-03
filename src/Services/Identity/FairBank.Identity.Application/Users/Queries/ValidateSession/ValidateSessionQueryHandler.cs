using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.ValidateSession;

public sealed class ValidateSessionQueryHandler(IUserRepository userRepository)
    : IRequestHandler<ValidateSessionQuery, bool>
{
    public async Task<bool> Handle(ValidateSessionQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null || !user.IsActive) return false;
        return user.IsSessionValid(request.SessionId);
    }
}
