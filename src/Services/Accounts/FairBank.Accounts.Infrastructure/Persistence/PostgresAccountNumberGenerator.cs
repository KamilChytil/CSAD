using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using Npgsql;

namespace FairBank.Accounts.Infrastructure.Persistence;

/// <summary>
/// Generates unique, sequential Czech-format account numbers using a PostgreSQL sequence.
/// Format: {prefix:D6}-{number:D10}/8888
/// - seq 1..9_999_999_999          → 000000-0000000001/8888 .. 000000-9999999999/8888
/// - seq 10_000_000_000..           → 000001-0000000001/8888 ...
/// </summary>
public sealed class PostgresAccountNumberGenerator(string connectionString) : IAccountNumberGenerator
{
    private const long MaxNumber = 9_999_999_999L;

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT nextval('accounts_service.account_number_seq')", conn);
        var seqVal = (long)(await cmd.ExecuteScalarAsync(ct))!;

        // seqVal 1..MaxNumber       → prefix=0, number=seqVal
        // seqVal MaxNumber+1..      → prefix=(seqVal-1)/MaxNumber, number=((seqVal-1)%MaxNumber)+1
        var prefix = (seqVal - 1) / MaxNumber;
        var number = ((seqVal - 1) % MaxNumber) + 1;

        return $"{prefix:D6}-{number:D10}/{AccountNumber.FairBankCode}";
    }
}
