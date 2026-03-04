using FairBank.Chat.Application;
using FairBank.Chat.Application.Conversations.Queries;
using FairBank.Chat.Application.Hubs;
using FairBank.Chat.Application.Messages.Queries.GetConversation;
using FairBank.Chat.Application.Messages.Queries.SearchMessages;
using FairBank.Chat.Application.Validation;
using FairBank.Chat.Domain.Entities;
using FairBank.Chat.Domain.Enums;
using FairBank.Chat.Infrastructure;
using FairBank.Chat.Infrastructure.Persistence;
using FairBank.SharedKernel.Security;
using FairBank.SharedKernel;
using Serilog;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Kafka(
        ctx.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
        ctx.Configuration["Kafka:Topic"] ?? "fairbank-logs"));

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
        policy.WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "https://localhost",
                "https://localhost:443",
                "http://web-app",
                "http://web-app:80",
                "http://api-gateway",
                "http://api-gateway:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Auto-create database tables on startup.
// EnsureCreatedAsync() does NOT create tables when the database already exists (shared DB).
// We check whether our tables exist and create them from the EF model if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();

    // Ensure schema + grants exist (init.sql only runs on first volume init)
    await using (var grantCmd = conn.CreateCommand())
    {
        grantCmd.CommandText = """
            CREATE SCHEMA IF NOT EXISTS chat_service;
            GRANT ALL PRIVILEGES ON SCHEMA chat_service TO CURRENT_USER;
            ALTER DEFAULT PRIVILEGES IN SCHEMA chat_service GRANT ALL ON TABLES TO CURRENT_USER;
            ALTER DEFAULT PRIVILEGES IN SCHEMA chat_service GRANT ALL ON SEQUENCES TO CURRENT_USER;
            """;
        await grantCmd.ExecuteNonQueryAsync();
    }

    await using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'chat_service' AND table_name = 'conversations')";
        var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
        if (!exists)
        {
            var script = db.Database.GenerateCreateScript();
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = script;
            await createCmd.ExecuteNonQueryAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.Title = "FairBank Chat API"; });
}

// Validate X-Internal-Api-Key on every inbound request (gateway → service auth)
app.UseMiddleware<ApiKeyMiddleware>();

app.UseCors("AllowSpecificOrigins");

app.UseSerilogRequestLogging();

// ── Health ─────────────────────────────────────────────────────────────────
app.MapGet("/health", async (ChatDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Chat" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Chat" }, statusCode: 503);
    }
});

// ── Conversations list ─────────────────────────────────────────────────────
// GET /api/v1/chat/conversations?userId=&role=&label=&parentId=
app.MapGet("/api/v1/chat/conversations", async (
    Guid userId,
    string role,
    string label,
    Guid? parentId,
    HttpContext httpContext,
    IMediator mediator) =>
{
    // BOLA: userId query param must match authenticated user (unless Banker/Admin)
    var authUserId = httpContext.GetUserId();
    var authRole = httpContext.GetUserRole();
    if (authRole != "Admin" && authRole != "Banker" && authUserId != userId)
        return Results.Json(new { error = "Forbidden" }, statusCode: 403);

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
}).RequireAuth();

// ── Ensure family conversation exists (called by parent when child is created) ─
// POST /api/v1/chat/conversations/family?parentId=&childId=&childLabel=
app.MapPost("/api/v1/chat/conversations/family", async (
    Guid parentId, Guid childId, string childLabel,
    HttpContext httpContext,
    FairBank.Chat.Domain.Ports.IConversationRepository convRepo) =>
{
    // BOLA: parentId must match authenticated user
    var authUserId = httpContext.GetUserId();
    if (authUserId != parentId)
        return Results.Json(new { error = "Forbidden" }, statusCode: 403);

    var conv = await convRepo.GetOrCreateFamilyAsync(parentId, childId, childLabel);
    return Results.Ok(new { conv.Id, conv.Label, conv.Type });
}).RequireAuth();

// ── Message history for a conversation ────────────────────────────────────
// GET /api/v1/chat/conversations/{id}/messages
app.MapGet("/api/v1/chat/conversations/{id:guid}/messages", async (Guid id, IMediator mediator) =>
{
    var messages = await mediator.Send(new GetConversationHistoryQuery(id));
    return Results.Ok(messages);
}).RequireAuth();

// ── BANKER TOOLS ────────────────────────────────────────────────────────
app.MapPost("/api/v1/chat/conversations/{id:guid}/assign", async (ISender sender, Guid id, [FromQuery] Guid bankerId, [FromQuery] string? bankerName) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.AssignBanker.AssignConversationCommand(id, bankerId, bankerName));
    return Results.NoContent();
}).RequireRole("Banker", "Admin");

app.MapPatch("/api/v1/chat/conversations/{id:guid}/notes", async (ISender sender, Guid id, [FromBody] string notes) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.UpdateNotes.UpdateConversationNotesCommand(id, notes));
    return Results.NoContent();
}).RequireRole("Banker", "Admin");

app.MapPost("/api/v1/chat/conversations/{id:guid}/close", async (ISender sender, Guid id) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.CloseConversation.CloseConversationCommand(id));
    return Results.NoContent();
}).RequireRole("Banker", "Admin");

app.MapPost("/api/v1/chat/conversations/{id:guid}/reopen", async (ISender sender, Guid id) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.ReopenConversation.ReopenConversationCommand(id));
    return Results.NoContent();
}).RequireRole("Banker", "Admin");

app.MapPost("/api/v1/chat/conversations/{id:guid}/transfer", async (ISender sender, Guid id, [FromQuery] Guid bankerId, [FromQuery] string? bankerName) =>
{
    await sender.Send(new FairBank.Chat.Application.Conversations.Commands.AssignBanker.AssignConversationCommand(id, bankerId, bankerName));
    return Results.NoContent();
}).RequireRole("Banker", "Admin");

// ── Unique clients for a specific banker ─────────────────────────────────
// GET /api/v1/chat/conversations/banker/{bankerId}/clients
app.MapGet("/api/v1/chat/conversations/banker/{bankerId:guid}/clients", async (Guid bankerId, HttpContext httpContext, ChatDbContext db) =>
{
    // BOLA: bankerId should match auth user if role is Banker
    var authUserId = httpContext.GetUserId();
    var authRole = httpContext.GetUserRole();
    if (authRole == "Banker" && authUserId != bankerId)
        return Results.Json(new { error = "Forbidden" }, statusCode: 403);

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
}).RequireRole("Banker", "Admin").WithTags("Chat");

// ── Message search ────────────────────────────────────────────────────────
app.MapGet("/api/v1/chat/conversations/{id:guid}/messages/search", async (
    Guid id, string query, int? page, int? pageSize, IMediator mediator) =>
{
    var result = await mediator.Send(new SearchMessagesQuery(id, query, page ?? 1, pageSize ?? 20));
    return Results.Ok(result);
}).RequireAuth();

// ── Attachment upload ─────────────────────────────────────────────────────
app.MapPost("/api/v1/chat/messages/{messageId:guid}/attachments", async (
    Guid messageId,
    IFormFile file,
    ChatDbContext db) =>
{
    // ── File size limit ──────────────────────────────────────────────────
    if (!FileValidator.IsFileSizeValid(file.Length))
        return Results.BadRequest(new { Error = "File size exceeds 10 MB limit." });

    // ── Content-type allowlist ───────────────────────────────────────────
    if (!FileValidator.IsContentTypeAllowed(file.ContentType))
        return Results.BadRequest(new { Error = "File type not allowed." });

    // ── File extension allowlist ─────────────────────────────────────────
    var ext = Path.GetExtension(file.FileName);
    if (!FileValidator.IsExtensionAllowed(ext))
        return Results.BadRequest(new { Error = "File extension not allowed." });

    // ── Magic byte validation ────────────────────────────────────────────
    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);
    var fileBytes = memoryStream.ToArray();

    if (!FileValidator.ValidateMagicBytes(fileBytes, file.ContentType))
        return Results.BadRequest(new { Error = "File content does not match declared type." });

    // ── Persist file (sanitized GUID name) ───────────────────────────────
    var uploadsDir = "/data/chat-attachments";
    Directory.CreateDirectory(uploadsDir);

    var fileName = $"{Guid.NewGuid()}{ext}";
    var filePath = Path.Combine(uploadsDir, fileName);

    await File.WriteAllBytesAsync(filePath, fileBytes);

    var attachment = ChatAttachment.Create(
        messageId, file.FileName, file.ContentType, file.Length, filePath);

    db.Set<ChatAttachment>().Add(attachment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/chat/attachments/{attachment.Id}", new
    {
        attachment.Id, attachment.FileName, attachment.ContentType, attachment.FileSize
    });
})
.RequireAuth()
.DisableAntiforgery();

// ── Attachment download ───────────────────────────────────────────────────
app.MapGet("/api/v1/chat/attachments/{id:guid}/download", async (Guid id, ChatDbContext db) =>
{
    var attachment = await db.Set<ChatAttachment>().FindAsync(id);
    if (attachment is null) return Results.NotFound();

    if (!File.Exists(attachment.StoragePath)) return Results.NotFound();

    // Derive a safe content-type from the stored file extension instead of
    // trusting the client-supplied ContentType (prevents stored-XSS via
    // crafted content-type headers).
    var safeContentType = Path.GetExtension(attachment.FileName)?.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };

    // Content-Disposition: attachment is set by Results.File when a
    // fileDownloadName is provided, which forces the browser to download
    // rather than render inline.
    return Results.File(attachment.StoragePath, safeContentType, attachment.FileName);
}).RequireAuth();

// ── SignalR Hub ────────────────────────────────────────────────────────────
app.MapHub<ChatHub>("/chat-hub");

app.Run();
