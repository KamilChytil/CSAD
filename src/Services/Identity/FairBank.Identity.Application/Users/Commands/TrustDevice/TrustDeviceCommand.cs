using MediatR;

namespace FairBank.Identity.Application.Users.Commands.TrustDevice;

public sealed record TrustDeviceCommand(Guid DeviceId, Guid UserId) : IRequest;
