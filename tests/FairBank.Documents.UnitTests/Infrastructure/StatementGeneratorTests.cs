using FluentAssertions;
using Xunit;
using FairBank.Documents.Infrastructure.Services;
using FairBank.Documents.Application.DTOs;
using System.Text;

namespace FairBank.Documents.UnitTests.Infrastructure;

public class StatementGeneratorTests
{
    [Fact]
    public async Task GeneratePdf_ShouldReturnNonEmptyBytes()
    {
        var generator = new StatementGenerator();
        var txs = new List<DocumentTransactionDto>
        {
            new(DateTime.UtcNow, "Deposit", 100, "CZK", "Test")
        };
        var result = await generator.GenerateAsync(Guid.NewGuid(), null, null, txs, FairBank.Documents.Application.Enums.StatementFormat.Pdf);
        result.Content.Should().NotBeEmpty();
        result.ContentType.Should().Be("application/pdf");
    }
}
