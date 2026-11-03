using System.Globalization;
using System.IO;
using System.Text;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public static class CheckStatusReport
{
    private const string Header = "Document Code,Revision,CP,Verdict,Back-Drafted,Markup Author,Markup Date UTC,Current Revision,Distribution,Issue Date,Issued By,Purpose,Method,Queue Reason,File Path,Scan Warning";

    public static IReadOnlyList<CheckPrintQueueRow> Run(string projectFolder, string checkingFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        var storagePath = Path.Combine(projectFolder, "project_data.json");
        if (!File.Exists(storagePath))
            throw new FileNotFoundException("Project data not found.", storagePath);

        var documents = ProjectStorage.Load(storagePath).Documents.Select(document =>
        {
            var metadata = new DocumentMetadata { DocumentNumber = document.DocumentNumber };
            foreach (var revision in document.RevisionHistory)
            {
                metadata.RevisionHistory[revision.Key] = new RevisionInfo
                {
                    Revision = revision.Value.Revision,
                    Purpose = revision.Value.Purpose,
                    Method = revision.Value.Method,
                    IssuedBy = revision.Value.IssuedBy,
                    IsDistributed = revision.Value.IsDistributed,
                    IsSuperseded = revision.Value.IsSuperseded || string.Equals(
                        revision.Value.Purpose,
                        RevisionInfo.SupersededPurpose,
                        StringComparison.OrdinalIgnoreCase),
                    FilePath = revision.Value.FilePath
                };
            }
            return metadata;
        }).ToList();

        return Render(CheckPrintApplier.Apply(CheckPrintScanner.Plan(checkingFolder)), documents);
    }

    public static IReadOnlyList<CheckPrintQueueRow> Render(
        ApplyResult result,
        IEnumerable<DocumentMetadata> documents)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(documents);
        return CheckPrintRegisterJoin.BuildLiveQueue(result.Facts, documents);
    }

    public static void WriteCsv(IEnumerable<CheckPrintQueueRow> rows, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        WriteCsv(rows, writer);
    }

    public static void WriteCsv(IEnumerable<CheckPrintQueueRow> rows, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Header);

        foreach (var row in rows)
        {
            var check = row.CheckPrint;
            writer.WriteLine(string.Join(",",
                Escape(check.DocumentCode),
                Escape(check.Revision),
                check.Cp.ToString(CultureInfo.InvariantCulture),
                Escape(check.StatusText),
                check.BackDrafted.ToString(),
                Escape(check.StampAuthor),
                check.StampDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                row.IsCurrent.ToString(),
                Escape(row.DistributionText),
                row.IssueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(row.RegisterRevision?.IssuedBy),
                Escape(row.RegisterRevision?.Purpose),
                Escape(row.RegisterRevision?.Method),
                Escape(row.QueueReason),
                Escape(check.FilePath),
                Escape(check.Issue)));
        }
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
