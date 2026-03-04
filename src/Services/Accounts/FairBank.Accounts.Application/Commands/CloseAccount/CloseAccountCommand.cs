using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CloseAccount;

public sealed record CloseAccountCommand(Guid AccountId) : IRequest<AccountResponse>;
