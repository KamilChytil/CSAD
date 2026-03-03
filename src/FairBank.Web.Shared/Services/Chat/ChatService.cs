using System.Net.Http.Json;
using FairBank.Web.Shared.Models.Chat;
using Microsoft.AspNetCore.SignalR.Client;

namespace FairBank.Web.Shared.Services.Chat;

public sealed class ChatService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private HubConnection? _hubConnection;

    public event Action<ChatMessageDto>? OnMessageReceived;

    public ChatService(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // ── Conversations list ─────────────────────────────────────────────────

    public async Task<IEnumerable<ConversationDto>> GetConversationsAsync(
        Guid userId, string role, string label, Guid? parentId = null)
    {
        var qs = $"api/v1/chat/conversations?userId={userId}&role={role}&label={Uri.EscapeDataString(label)}";
        if (parentId.HasValue) qs += $"&parentId={parentId}";
        try
        {
            return await _http.GetFromJsonAsync<IEnumerable<ConversationDto>>(qs)
                   ?? Enumerable.Empty<ConversationDto>();
        }
        catch { return Enumerable.Empty<ConversationDto>(); }
    }

    // ── Message history ────────────────────────────────────────────────────

    public async Task<IEnumerable<ChatMessageDto>> GetConversationMessagesAsync(Guid conversationId)
    {
        try
        {
            return await _http.GetFromJsonAsync<IEnumerable<ChatMessageDto>>(
                       $"api/v1/chat/conversations/{conversationId}/messages")
                   ?? Enumerable.Empty<ChatMessageDto>();
        }
        catch { return Enumerable.Empty<ChatMessageDto>(); }
    }

    // ── SignalR ────────────────────────────────────────────────────────────

    public async Task InitializeAsync(string token)
    {
        if (_hubConnection is not null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/chat-hub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("ReceiveMessage", obj =>
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                var msg = System.Text.Json.JsonSerializer.Deserialize<ChatMessageDto>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg is not null) OnMessageReceived?.Invoke(msg);
            }
            catch { }
        });

        await _hubConnection.StartAsync();
    }

    public async Task JoinConversationAsync(Guid conversationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinConversation", conversationId.ToString());
    }

    public async Task LeaveConversationAsync(Guid conversationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("LeaveConversation", conversationId.ToString());
    }

    public async Task SendMessageAsync(Guid conversationId, Guid senderId, string senderName, string content)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SendMessage", conversationId, senderId, senderName, content);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
