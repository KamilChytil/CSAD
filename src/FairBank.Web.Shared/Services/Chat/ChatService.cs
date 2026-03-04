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
    Task AssignConversationAsync(Guid conversationId, Guid bankerId, string? bankerName = null);
    Task UpdateConversationNotesAsync(Guid conversationId, string notes);
    Task TransferChatAsync(Guid conversationId, Guid targetBankerId, string? targetBankerName = null);
    Task CloseConversationAsync(Guid conversationId);
    Task ReopenConversationAsync(Guid conversationId);
    Task<IEnumerable<BankerDto>> GetBankersAsync();
}

public sealed class ChatService : IChatService, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private HubConnection? _hubConnection;

    private string? _token;

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

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Array.Empty<ConversationDto>();
        
        return await response.Content.ReadFromJsonAsync<IEnumerable<ConversationDto>>() ?? Array.Empty<ConversationDto>();
    }

    // ── Message history ────────────────────────────────────────────────────

    public async Task<IEnumerable<ChatMessageDto>> GetConversationMessagesAsync(Guid conversationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chat/conversations/{conversationId}/messages");
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Array.Empty<ChatMessageDto>();
        
        return await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>() ?? Array.Empty<ChatMessageDto>();
    }

    // Banker tool methods implementations
    public async Task AssignConversationAsync(Guid conversationId, Guid bankerId, string? bankerName = null)
    {
        var url = $"/api/v1/chat/conversations/{conversationId}/assign?bankerId={bankerId}";
        if (!string.IsNullOrEmpty(bankerName)) url += $"&bankerName={Uri.EscapeDataString(bankerName)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _httpClient.SendAsync(request);
    }

    public async Task UpdateConversationNotesAsync(Guid conversationId, string notes)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/chat/conversations/{conversationId}/notes");
        request.Content = JsonContent.Create(notes);
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _httpClient.SendAsync(request);
    }

    public async Task TransferChatAsync(Guid conversationId, Guid targetBankerId, string? targetBankerName = null)
    {
        var url = $"/api/v1/chat/conversations/{conversationId}/transfer?bankerId={targetBankerId}";
        if (!string.IsNullOrEmpty(targetBankerName)) url += $"&bankerName={Uri.EscapeDataString(targetBankerName)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _httpClient.SendAsync(request);
    }

    public async Task CloseConversationAsync(Guid conversationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/chat/conversations/{conversationId}/close");
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _httpClient.SendAsync(request);
    }

    public async Task ReopenConversationAsync(Guid conversationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/chat/conversations/{conversationId}/reopen");
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _httpClient.SendAsync(request);
    }

    public async Task<IEnumerable<BankerDto>> GetBankersAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/bankers");
        if (_token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Array.Empty<BankerDto>();
        
        return await response.Content.ReadFromJsonAsync<IEnumerable<BankerDto>>() ?? Array.Empty<BankerDto>();
    }

    // ── SignalR ────────────────────────────────────────────────────────────

    public async Task InitializeAsync(string token)
    {
        _token = token;
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

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _hubConnection.StartAsync(cts.Token);
        }
        catch
        {
            // SignalR hub unreachable — chat will be unavailable but app won't hang
        }
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
