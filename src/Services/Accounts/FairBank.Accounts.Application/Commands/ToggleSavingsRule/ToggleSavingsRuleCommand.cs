using MediatR;

namespace FairBank.Accounts.Application.Commands.ToggleSavingsRule;

public sealed record ToggleSavingsRuleCommand(Guid RuleId) : IRequest;
