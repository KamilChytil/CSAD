using System.Net.Http.Json;
using FairBank.Web.Shared.Models.Chat;
using Microsoft.AspNetCore.SignalR.Client;

namespace FairBank.Web.Shared.Services.Chat;

public interface IChatService
{
    event Action<ChatMessageDto>? OnMessageReceived;
    Task InitializeAsync(string token);
    Task<IEnumerable<ConversationDto>> GetConversationsAsync(Guid userId, string role, string label, Guid? parentId = null);
    Task<IEnumerable<ChatMessageDto>> GetConversationMessagesAsync(Guid conversationId);
    Task JoinConversationAsync(Guid conversationId);
    Task LeaveConversationAsync(Guid conversationId);
    Task SendMessageAsync(Guid conversationId, Guid senderId, string senderName, string content);
    
    // Banker tool methods
    Task AssignConversationAsync(Guid conversationId, Guid bankerId);
    Task UpdateConversationNotesAsync(Guid conversationId, string notes);
    Task CloseConversationAsync(Guid conversationId);
    Task ReopenConversationAsync(Guid conversationId);
}

public sealed class ChatService : IChatService, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private HubConnection? _hubConnection;

    public event Action<ChatMessageDto>? OnMessageReceived;

    public ChatService(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // ── Conversations list ─────────────────────────────────────────────────

    public async Task<IEnumerable<ConversationDto>> GetConversationsAsync(
        Guid userId, string role, string label, Guid? parentId = null)
    {
        var url = $"/api/v1/chat/conversations?userId={userId}&role={Uri.EscapeDataString(role)}&label={Uri.EscapeDataString(label ?? "")}";
        if (parentId.HasValue)
            url += $"&parentId={parentId.Value}";

        var response = await _httpClient.GetFromJsonAsync<IEnumerable<ConversationDto>>(url);
        return response ?? Array.Empty<ConversationDto>();
    }

    // ── Message history ────────────────────────────────────────────────────

    public async Task<IEnumerable<ChatMessageDto>> GetConversationMessagesAsync(Guid conversationId)
    {
        var response = await _httpClient.GetFromJsonAsync<IEnumerable<ChatMessageDto>>($"/api/v1/chat/conversations/{conversationId}/messages");
        return response ?? Array.Empty<ChatMessageDto>();
    }

    // Banker tool methods implementations
    public async Task AssignConversationAsync(Guid conversationId, Guid bankerId)
    {
        await _httpClient.PostAsync($"/api/v1/chat/conversations/{conversationId}/assign?bankerId={bankerId}", null);
    }

    public async Task UpdateConversationNotesAsync(Guid conversationId, string notes)
    {
        await _httpClient.PatchAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/notes", notes);
    }

    public async Task TransferChatAsync(Guid conversationId, Guid targetBankerId)
    {
        await _httpClient.PostAsync($"/api/v1/chat/conversations/{conversationId}/transfer?bankerId={targetBankerId}", null);
    }

    public async Task CloseConversationAsync(Guid conversationId)
    {
        await _httpClient.PostAsync($"/api/v1/chat/conversations/{conversationId}/close", null);
    }

    public async Task ReopenConversationAsync(Guid conversationId)
    {
        await _httpClient.PostAsync($"/api/v1/chat/conversations/{conversationId}/reopen", null);
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
