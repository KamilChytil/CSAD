using FairBank.Chat.Domain.Aggregates;
using FairBank.SharedKernel.Domain;

namespace FairBank.Chat.Domain.Ports;

public interface IChatRepository : IRepository<ChatMessage, Guid>
{
    Task<IEnumerable<ChatMessage>> GetMessagesBetweenUsersAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default);
}
