using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetSecuritySettings;

public sealed record GetSecuritySettingsQuery(Guid UserId) : IRequest<SecuritySettingsResponse?>;
