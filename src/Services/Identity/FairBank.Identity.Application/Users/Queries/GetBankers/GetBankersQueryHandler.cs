using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetBankers;

public sealed class GetBankersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetBankersQuery, IEnumerable<UserResponse>>
{
    public async Task<IEnumerable<UserResponse>> Handle(GetBankersQuery request, CancellationToken ct)
    {
        var users = await userRepository.GetAllAsync(ct);
        return users
            .Where(u => u.Role == UserRole.Banker || u.Role == UserRole.Admin)
            .Select(u => new UserResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email.Value,
                u.Role,
                u.IsActive,
                u.CreatedAt));
    }
}
