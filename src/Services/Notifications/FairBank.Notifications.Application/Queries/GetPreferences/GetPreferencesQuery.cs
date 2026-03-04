using FairBank.Notifications.Application.DTOs;
using MediatR;

namespace FairBank.Notifications.Application.Queries.GetPreferences;

public sealed record GetPreferencesQuery(Guid UserId) : IRequest<NotificationPreferenceResponse>;
