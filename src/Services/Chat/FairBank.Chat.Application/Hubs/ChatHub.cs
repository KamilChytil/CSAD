using FairBank.Chat.Application.Messages.Commands.SendMessage;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace FairBank.Chat.Application.Hubs;

public sealed class ChatHub(ISender sender) : Hub
{
    public async Task SendMessage(Guid receiverId, string content)
    {
        // For simplicity, we assume the user is authenticated and we can get their ID from claims
        // In a real app, we'd use Context.UserIdentifier
        var senderIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        
        if (senderIdClaim == null)
        {
            // Placeholder: for university project demo, we might need a way to pass senderId
            // but for now, we'll try to use the name identifier if available.
            return;
        }

        var senderId = Guid.Parse(senderIdClaim.Value);
        
        var command = new SendMessageCommand(senderId, receiverId, content);
        await sender.Send(command);

        // Notify both sender and receiver
        await Clients.Users(senderId.ToString(), receiverId.ToString())
            .SendAsync("ReceiveMessage", senderId, content);
    }

    public async Task JoinConversation(Guid otherUserId)
    {
        // Optional: logic for tracking who is talking to whom
    }
}
