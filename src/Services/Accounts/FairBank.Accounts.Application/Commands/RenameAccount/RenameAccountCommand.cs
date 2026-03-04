using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RenameAccount;

public sealed record RenameAccountCommand(Guid AccountId, string? Alias) : IRequest<AccountResponse>;
