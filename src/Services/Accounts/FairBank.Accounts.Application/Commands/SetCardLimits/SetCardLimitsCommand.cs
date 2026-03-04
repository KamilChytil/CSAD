using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetCardLimits;

public sealed record SetCardLimitsCommand(
    Guid CardId,
    decimal? DailyLimit,
    decimal? MonthlyLimit,
    Currency Currency) : IRequest;
