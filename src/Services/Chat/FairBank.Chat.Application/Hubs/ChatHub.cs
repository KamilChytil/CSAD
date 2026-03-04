using FairBank.Chat.Application.Messages.Commands.MarkMessageRead;
using FairBank.Chat.Application.Messages.Commands.SendMessage;
using FairBank.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace FairBank.Chat.Application.Hubs;

public sealed class ChatHub(ISender sender) : Hub
{
    /// <summary>Client joins the SignalR group for a conversation room.</summary>
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
    }

    /// <summary>Client leaves a conversation room.</summary>
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
    }

    /// <summary>Send a message to a conversation room. All group members receive it in real time.</summary>
    public async Task SendMessage(Guid conversationId, Guid senderId, string senderName, string content)
    {
        var command = new SendMessageCommand(conversationId, senderId, senderName, content);
        var saved = await sender.Send(command);

        // Broadcast to everyone in the conversation group (including sender for echo)
        await Clients.Group($"conv-{conversationId}")
            .SendAsync("ReceiveMessage", new
            {
                saved.Id,
                saved.ConversationId,
                saved.SenderId,
                saved.SenderName,
                saved.Content,
                saved.SentAt
            });
    }

    /// <summary>Notify other participants that a user started typing.</summary>
    public async Task StartTyping(Guid conversationId, Guid userId, string userName)
    {
        await Clients.OthersInGroup($"conv-{conversationId}")
            .SendAsync("UserTyping", new { userId, userName });
    }

    /// <summary>Notify other participants that a user stopped typing.</summary>
    public async Task StopTyping(Guid conversationId, Guid userId)
    {
        await Clients.OthersInGroup($"conv-{conversationId}")
            .SendAsync("UserStoppedTyping", new { userId });
    }

    /// <summary>Mark a message as read and notify other participants.</summary>
    public async Task MarkMessageAsRead(Guid conversationId, Guid messageId, Guid userId)
    {
        var command = new MarkMessageReadCommand(messageId);
        await sender.Send(command);

        await Clients.OthersInGroup($"conv-{conversationId}")
            .SendAsync("MessageRead", new { messageId, userId, readAt = DateTime.UtcNow });
    }
}
