using MediatR;

namespace FairBank.Identity.Application.Users.Queries.ValidateSession;

/// <summary>Returns true if the session is still valid (matches active session on user record).</summary>
public sealed record ValidateSessionQuery(Guid UserId, Guid SessionId) : IRequest<bool>;
