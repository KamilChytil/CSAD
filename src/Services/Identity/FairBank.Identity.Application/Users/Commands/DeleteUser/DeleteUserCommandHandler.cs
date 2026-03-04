using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.SoftDelete();

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        auditLogger.LogSecurityEvent("DeleteUser", "Success", request.UserId);

        // TODO: Cascade soft delete to other services.
        // When a user is soft-deleted, their resources in other services should be deactivated:
        //   - Cards service: POST http://cards-api:8080/api/v1/cards/user/{userId}/deactivate-all
        //     (blocks all active cards for the user)
        //   - Payments service: POST http://payments-api:8080/api/v1/payment-templates/deactivate-all
        //     (soft-deletes all payment templates; requires resolving user's account IDs via Accounts service first)
        // This requires injecting IHttpClientFactory and registering named HttpClients in DI.
        // Consider using an async messaging approach (e.g., domain events via Kafka) for better reliability.
    }
}
