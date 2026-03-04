using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetChildren;

public sealed class GetChildrenQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetChildrenQuery, IReadOnlyList<UserResponse>>
{
    public async Task<IReadOnlyList<UserResponse>> Handle(GetChildrenQuery request, CancellationToken ct)
    {
        var children = await userRepository.GetChildrenAsync(request.ParentId, ct);

        return children.Select(c => new UserResponse(
            c.Id, c.FirstName, c.LastName,
            c.Email.Value, c.Role, c.IsActive, c.CreatedAt,
            ParentId: c.ParentId)).ToList();
    }
}
