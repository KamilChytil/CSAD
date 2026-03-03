using FairBank.Chat.Application;
using FairBank.Chat.Application.Hubs;
using FairBank.Chat.Infrastructure;
using FairBank.Chat.Infrastructure.Persistence;
using FairBank.Chat.Application.Messages.Queries.GetConversation;
using MediatR;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(connectionString);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "FairBank Chat API";
    });
}

app.UseCors("AllowAll");

app.MapGet("/health", () => new { Status = "Healthy", Service = "Chat" });

// API Endpoints
app.MapGet("/api/v1/chat/history/{user1Id:guid}/{user2Id:guid}", async (Guid user1Id, Guid user2Id, IMediator mediator) =>
{
    var messages = await mediator.Send(new GetConversationHistoryQuery(user1Id, user2Id));
    return Results.Ok(messages);
});

app.MapHub<ChatHub>("/chat-hub");

app.Run();
