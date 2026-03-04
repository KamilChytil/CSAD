using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RevokeDevice;

public sealed record RevokeDeviceCommand(Guid DeviceId, Guid UserId) : IRequest;
