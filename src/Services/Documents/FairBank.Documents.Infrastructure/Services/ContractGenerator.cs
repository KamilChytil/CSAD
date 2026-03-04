using System.Text;
using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;
using FairBank.Documents.Application.Ports;

namespace FairBank.Documents.Infrastructure.Services;

public sealed class ContractGenerator : IContractGenerator
{
    public Task<ContractResponse> GenerateAsync(ProductContractDto contract, ContractFormat format)
    {
        var text = new StringBuilder();
        text.AppendLine("SMLOUVA O PRODUKT");
        text.AppendLine($"Číslo žádosti: {contract.ApplicationId}");
        text.AppendLine($"Klient: {contract.UserName} ({contract.UserEmail})");
        text.AppendLine($"Produkt: {contract.ProductType}");
        text.AppendLine($"Stav: {contract.Status}");
        text.AppendLine($"Měsíční splátka: {contract.MonthlyPayment:N2} CZK");
        text.AppendLine($"Datum: {contract.CreatedAt:dd.MM.yyyy}");
        text.AppendLine();
        text.AppendLine("Parametry: " + contract.Parameters);

        var bytes = Encoding.UTF8.GetBytes(text.ToString());
        var (contentType, ext) = format switch
        {
            ContractFormat.Pdf  => ("application/pdf", "pdf"),
            ContractFormat.Docx => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
            _ => ("text/plain", "txt")
        };

        return Task.FromResult(new ContractResponse(
            bytes,
            contentType,
            $"smlouva-{contract.ApplicationId}.{ext}"));
    }
}
