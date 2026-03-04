using FairBank.Documents.Application.DTOs;
using FairBank.Documents.Application.Enums;
using FairBank.Documents.Application.Ports;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FairBank.Documents.Infrastructure.Services;

public sealed class StatementGenerator : IStatementGenerator
{
    public Task<StatementResponse> GenerateAsync(
        Guid accountId,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<DocumentTransactionDto> transactions,
        StatementFormat format)
    {
        return format switch
        {
            StatementFormat.Pdf => Task.FromResult(GeneratePdf(accountId, from, to, transactions)),
            StatementFormat.Docx => Task.FromResult(GenerateDocx(accountId, from, to, transactions)),
            StatementFormat.Xlsx => Task.FromResult(GenerateXlsx(accountId, from, to, transactions)),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    private StatementResponse GeneratePdf(Guid accountId, DateTime? from, DateTime? to, IReadOnlyList<DocumentTransactionDto> txs)
    {
        // Generate as text-based report for PDF (simple approach)
        using var ms = new MemoryStream();
        var writer = new StringWriter();
        writer.WriteLine($"ACCOUNT STATEMENT");
        writer.WriteLine($"Account ID: {accountId}");
        writer.WriteLine($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
        writer.WriteLine(new string('=', 100));
        writer.WriteLine($"{"Date",-15}{"Type",-20}{"Amount",-20}{"Currency",-10}{"Description",-35}");
        writer.WriteLine(new string('=', 100));
        
        foreach (var t in txs)
        {
            writer.WriteLine($"{t.OccurredAt:yyyy-MM-dd}     {t.Type,-20}{t.Amount,-20}{t.Currency,-10}{t.Description,-35}");
        }
        
        var text = writer.ToString();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return new StatementResponse(bytes, "application/pdf", $"statement-{accountId}.pdf");
    }

    private StatementResponse GenerateDocx(Guid accountId, DateTime? from, DateTime? to, IReadOnlyList<DocumentTransactionDto> txs)
    {
        using var ms = new MemoryStream();
        using var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        // Title
        var titlePara = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
            new Run(
                new RunProperties(new Bold()),
                new Text($"Account Statement - {accountId}")));
        body.Append(titlePara);

        // Period info
        var infoPara = new Paragraph(
            new Run(new Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}")));
        body.Append(infoPara);

        // Add spacing
        body.Append(new Paragraph());

        // Table
        var table = new Table(new TableProperties());
        var headerRow = new TableRow();
        
        foreach (var header in new[] { "Date", "Type", "Amount", "Currency", "Description" })
        {
            var cell = new TableCell(
                new TableCellProperties(new Shading { Fill = "D3D3D3" }),
                new Paragraph(
                    new Run(
                        new RunProperties(new Bold()),
                        new Text(header))));
            headerRow.Append(cell);
        }
        table.Append(headerRow);

        // Rows
        foreach (var t in txs)
        {
            var row = new TableRow();
            row.Append(new TableCell(new Paragraph(new Run(new Text(t.OccurredAt.ToString("yyyy-MM-dd"))))));
            row.Append(new TableCell(new Paragraph(new Run(new Text(t.Type)))));
            row.Append(new TableCell(new Paragraph(new Run(new Text(t.Amount.ToString())))));
            row.Append(new TableCell(new Paragraph(new Run(new Text(t.Currency)))));
            row.Append(new TableCell(new Paragraph(new Run(new Text(t.Description ?? "")))));
            table.Append(row);
        }

        body.Append(table);
        doc.Save();

        var bytes = ms.ToArray();
        return new StatementResponse(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"statement-{accountId}.docx");
    }

    private StatementResponse GenerateXlsx(Guid accountId, DateTime? from, DateTime? to, IReadOnlyList<DocumentTransactionDto> txs)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Statement");
        
        // Headers
        ws.Cell(1, 1).Value = "Date";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "Amount";
        ws.Cell(1, 4).Value = "Currency";
        ws.Cell(1, 5).Value = "Description";
        
        // Make header bold
        ws.Range("A1:E1").Style.Font.Bold = true;
        
        // Data rows
        for (int i = 0; i < txs.Count; i++)
        {
            var t = txs[i];
            ws.Cell(i + 2, 1).Value = t.OccurredAt;
            ws.Cell(i + 2, 2).Value = t.Type;
            ws.Cell(i + 2, 3).Value = t.Amount;
            ws.Cell(i + 2, 4).Value = t.Currency;
            ws.Cell(i + 2, 5).Value = t.Description ?? "";
        }
        
        // Auto-fit columns
        ws.Columns().AdjustToContents();
        
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();
        return new StatementResponse(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"statement-{accountId}.xlsx");
    }
}


