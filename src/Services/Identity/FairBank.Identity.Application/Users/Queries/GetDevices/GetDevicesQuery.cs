using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetDevices;

public sealed record GetDevicesQuery(Guid UserId) : IRequest<IReadOnlyList<DeviceResponse>>;
