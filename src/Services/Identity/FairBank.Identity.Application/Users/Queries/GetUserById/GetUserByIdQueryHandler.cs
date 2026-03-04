using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, UserResponse?>
{
    public async Task<UserResponse?> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.Id, ct);

        if (user is null) return null;

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.PersonalIdNumber,
            user.DateOfBirth,
            user.PhoneNumber?.Value,
            user.Address?.Street,
            user.Address?.City,
            user.Address?.ZipCode,
            user.Address?.Country,
            user.IsEmailVerified,
            user.ParentId);
    }
}
