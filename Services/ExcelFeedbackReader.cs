using System.IO.Compression;
using System.Xml.Linq;

namespace POCLeituradeVozCliente.Services;

public class ExcelFeedbackReader : IExcelFeedbackReader
{
    public List<string> ReadFirstColumnValues(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Arquivo Excel nao encontrado.", filePath);
        }

        using var archive = ZipFile.OpenRead(filePath);

        var sharedStrings = LoadSharedStrings(archive);
        var worksheetPath = ResolveFirstWorksheetPath(archive);
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidOperationException("Nao foi possivel localizar a primeira planilha do arquivo Excel.");

        using var worksheetStream = worksheetEntry.Open();
        var worksheet = XDocument.Load(worksheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var values = new List<string>();
        var rows = worksheet.Descendants(spreadsheet + "row");

        foreach (var row in rows)
        {
            var firstColumnCell = row.Elements(spreadsheet + "c")
                .FirstOrDefault(cell => IsFirstColumn(cell.Attribute("r")?.Value));

            if (firstColumnCell is null)
            {
                continue;
            }

            var cellValue = ReadCellValue(firstColumnCell, sharedStrings, spreadsheet);
            if (!string.IsNullOrWhiteSpace(cellValue))
            {
                values.Add(cellValue.Trim());
            }
        }

        return values;
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        return document.Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("Nao foi possivel localizar o workbook do arquivo Excel.");
        var workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("Nao foi possivel localizar os relacionamentos do workbook.");

        using var workbookStream = workbookEntry.Open();
        using var workbookRelsStream = workbookRelsEntry.Open();

        var workbook = XDocument.Load(workbookStream);
        var relationships = XDocument.Load(workbookRelsStream);

        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace officeDocumentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        var firstSheet = workbook.Descendants(spreadsheet + "sheet").FirstOrDefault()
            ?? throw new InvalidOperationException("Nenhuma planilha foi encontrada no arquivo Excel.");
        var relationshipId = firstSheet.Attribute(officeDocumentRelationships + "id")?.Value
            ?? throw new InvalidOperationException("Nao foi possivel localizar o relacionamento da primeira planilha.");

        var target = relationships.Descendants(packageRelationships + "Relationship")
            .FirstOrDefault(rel => rel.Attribute("Id")?.Value == relationshipId)
            ?.Attribute("Target")?.Value
            ?? throw new InvalidOperationException("Nao foi possivel resolver o caminho da primeira planilha.");

        return target.StartsWith("/")
            ? $"xl{target}"
            : $"xl/{target}";
    }

    private static bool IsFirstColumn(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return false;
        }

        return cellReference.StartsWith("A", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace spreadsheet)
    {
        var dataType = cell.Attribute("t")?.Value;
        var value = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;

        if (dataType == "s" && int.TryParse(value, out var sharedStringIndex) && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        if (dataType == "inlineStr")
        {
            return string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value));
        }

        return value;
    }
}
