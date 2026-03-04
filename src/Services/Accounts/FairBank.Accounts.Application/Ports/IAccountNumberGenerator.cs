namespace FairBank.Accounts.Application.Ports;

/// <summary>Generates unique, sequential Czech-format account numbers.</summary>
public interface IAccountNumberGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
