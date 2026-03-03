using Microsoft.AspNetCore.SignalR.Client;
using FairBank.Web.Shared.Models.Chat;
using FairBank.Web.Shared.Services;
using System.Net.Http.Json;

namespace FairBank.Web.Shared.Services.Chat;

public sealed class ChatService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private HubConnection? _hubConnection;
    private readonly string _chatApiUrl;

    public ChatService(HttpClient http, string chatApiUrl)
    {
        _http = http;
        _chatApiUrl = chatApiUrl;
    }

    public event Action<ChatMessageDto>? OnMessageReceived;

    public async Task InitializeAsync(string token)
    {
        var url = _chatApiUrl.TrimEnd('/') + "/chat-hub";
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid, string>("ReceiveMessage", (senderId, content) =>
        {
            var message = new ChatMessageDto(Guid.NewGuid(), senderId, Guid.Empty, content, DateTime.UtcNow);
            OnMessageReceived?.Invoke(message);
        });

        await _hubConnection.StartAsync();
    }

    public async Task SendMessageAsync(Guid receiverId, string content)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("SendMessage", receiverId, content);
        }
    }

    public async Task<IEnumerable<ChatMessageDto>> GetConversationHistoryAsync(Guid currentUserId, Guid otherUserId)
    {
        try
        {
            return await _http.GetFromJsonAsync<IEnumerable<ChatMessageDto>>($"api/v1/chat/history/{currentUserId}/{otherUserId}") 
                   ?? Enumerable.Empty<ChatMessageDto>();
        }
        catch
        {
            return Enumerable.Empty<ChatMessageDto>();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
