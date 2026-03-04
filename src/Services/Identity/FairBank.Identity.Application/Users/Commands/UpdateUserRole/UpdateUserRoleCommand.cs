using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(Guid UserId, UserRole NewRole) : IRequest;
