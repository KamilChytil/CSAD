using FairBank.Chat.Application;
using FairBank.Chat.Application.Conversations.Queries;
using FairBank.Chat.Application.Hubs;
using FairBank.Chat.Application.Messages.Queries.GetConversation;
using FairBank.Chat.Application.Messages.Queries.SearchMessages;
using FairBank.Chat.Domain.Entities;
using FairBank.Chat.Domain.Enums;
using FairBank.Chat.Infrastructure;
using FairBank.Chat.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials());
});

var app = builder.Build();

// Ensure DB schema exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.Title = "FairBank Chat API"; });
}

app.UseCors("AllowAll");

// ── Health ─────────────────────────────────────────────────────────────────
app.MapGet("/health", () => new { Status = "Healthy", Service = "Chat" });

// ── Conversations list ─────────────────────────────────────────────────────
// GET /api/v1/chat/conversations?userId=&role=&label=&parentId=
app.MapGet("/api/v1/chat/conversations", async (
    Guid userId,
    string role,
    string label,
    Guid? parentId,
    IMediator mediator) =>
{
    IEnumerable<FairBank.Chat.Application.Messages.DTOs.ConversationSummaryDto> result;

    if (role is "Parent")
    {
        // Parent: their own Support conversation + one Family room per child
        var support = await mediator.Send(new GetConversationsQuery(userId, role, label, parentId));
        var family  = await mediator.Send(new GetParentConversationsQuery(userId));
        result = support.Concat(family);
    }
    else
    {
        result = await mediator.Send(new GetConversationsQuery(userId, role, label, parentId));
    }

    return Results.Ok(result);
});

// ── Ensure family conversation exists (called by parent when child is created) ─
// POST /api/v1/chat/conversations/family?parentId=&childId=&childLabel=
app.MapPost("/api/v1/chat/conversations/family", async (
    Guid parentId, Guid childId, string childLabel,
    FairBank.Chat.Domain.Ports.IConversationRepository convRepo) =>
{
    var conv = await convRepo.GetOrCreateFamilyAsync(parentId, childId, childLabel);
    return Results.Ok(new { conv.Id, conv.Label, conv.Type });
});

// ── Message history for a conversation ────────────────────────────────────
// GET /api/v1/chat/conversations/{id}/messages
app.MapGet("/api/v1/chat/conversations/{id:guid}/messages", async (Guid id, IMediator mediator) =>
{
    var messages = await mediator.Send(new GetConversationHistoryQuery(id));
    return Results.Ok(messages);
});

// ── BANKER TOOLS ────────────────────────────────────────────────────────
app.MapPost("/api/v1/chat/conversations/{id:guid}/assign", async (ISender sender, Guid id, [FromQuery] Guid bankerId) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.AssignBanker.AssignConversationCommand(id, bankerId));
    return Results.NoContent();
});

app.MapPatch("/api/v1/chat/conversations/{id:guid}/notes", async (ISender sender, Guid id, [FromBody] string notes) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.UpdateNotes.UpdateConversationNotesCommand(id, notes));
    return Results.NoContent();
});

app.MapPost("/api/v1/chat/conversations/{id:guid}/close", async (ISender sender, Guid id) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.CloseConversation.CloseConversationCommand(id));
    return Results.NoContent();
});

app.MapPost("/api/v1/chat/conversations/{id:guid}/reopen", async (ISender sender, Guid id) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.ReopenConversation.ReopenConversationCommand(id));
    return Results.NoContent();
});

app.MapPost("/api/v1/chat/conversations/{id:guid}/transfer", async (ISender sender, Guid id, [FromQuery] Guid bankerId) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.AssignBanker.AssignConversationCommand(id, bankerId));
    return Results.NoContent();
});

// ── Unique clients for a specific banker ─────────────────────────────────
// GET /api/v1/chat/conversations/banker/{bankerId}/clients
app.MapGet("/api/v1/chat/conversations/banker/{bankerId:guid}/clients", async (Guid bankerId, ChatDbContext db) =>
{
    var clients = await db.Conversations
        .Where(c => c.BankerOrParentId == bankerId && c.Type == ConversationType.Support)
        .GroupBy(c => c.ClientOrChildId)
        .Select(g => new
        {
            ClientId = g.Key,
            ClientLabel = g.First().Label,
            ActiveChatsCount = g.Count(c => c.Status == ConversationStatus.Active),
            LastActivity = g.Max(c => c.LastClientMessageAt)
        })
        .ToListAsync();
    return Results.Ok(clients);
}).WithTags("Chat");

// ── Message search ────────────────────────────────────────────────────────
app.MapGet("/api/v1/chat/conversations/{id:guid}/messages/search", async (
    Guid id, string query, int? page, int? pageSize, IMediator mediator) =>
{
    var result = await mediator.Send(new SearchMessagesQuery(id, query, page ?? 1, pageSize ?? 20));
    return Results.Ok(result);
});

// ── Attachment upload ─────────────────────────────────────────────────────
app.MapPost("/api/v1/chat/messages/{messageId:guid}/attachments", async (
    Guid messageId,
    IFormFile file,
    ChatDbContext db) =>
{
    var uploadsDir = "/data/chat-attachments";
    Directory.CreateDirectory(uploadsDir);

    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var filePath = Path.Combine(uploadsDir, fileName);

    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    var attachment = ChatAttachment.Create(
        messageId, file.FileName, file.ContentType, file.Length, filePath);

    db.Set<ChatAttachment>().Add(attachment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/chat/attachments/{attachment.Id}", new
    {
        attachment.Id, attachment.FileName, attachment.ContentType, attachment.FileSize
    });
})
.DisableAntiforgery();

// ── Attachment download ───────────────────────────────────────────────────
app.MapGet("/api/v1/chat/attachments/{id:guid}/download", async (Guid id, ChatDbContext db) =>
{
    var attachment = await db.Set<ChatAttachment>().FindAsync(id);
    if (attachment is null) return Results.NotFound();

    if (!File.Exists(attachment.StoragePath)) return Results.NotFound();

    return Results.File(attachment.StoragePath, attachment.ContentType, attachment.FileName);
});

// ── SignalR Hub ────────────────────────────────────────────────────────────
app.MapHub<ChatHub>("/chat-hub");

app.Run();
