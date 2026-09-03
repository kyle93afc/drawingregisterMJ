using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DrawingRegister.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using Colors = QuestPDF.Helpers.Colors;
using IContainer = QuestPDF.Infrastructure.IContainer;
using DocumentMetadata = DrawingRegister.App.Models.DocumentMetadata;

namespace DrawingRegister.App.Services;

public enum RegisterReportMode
{
    Register,
    Transmittal,
    DocReg
}

public sealed record RegisterReportRequest(
    RegisterReportMode Mode,
    DateTime ReportDate,
    string? ProjectNumber,
    string? ProjectName,
    string? Discipline,
    string? RegisterNumber,
    string? ClientNumber,
    IReadOnlyList<DocumentMetadata> Documents,
    DateTime? SelectedIssueDate = null,
    string? SelectedSubfolderName = null,
    string? SelectedSubfolderPath = null,
    string? DistributionText = null,
    string? PurposeOfIssue = null,
    string? MethodOfIssue = null,
    string? IssuedBy = null,
    string? TransmittalNumber = null);

public sealed record RegisterReportResult(
    string ActualFilePath,
    int PageCount);

public static class RegisterReportGenerator
{
    public static string GetSuggestedFileName(RegisterReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = BuildIdentity(request);
        var fileName = identity.FileNamePrefix;

        if (identity.IsTransmittal && !string.IsNullOrWhiteSpace(request.SelectedSubfolderName))
        {
            fileName += $"_{request.SelectedSubfolderName.Trim().Replace(" ", "_")}";
        }

        return fileName;
    }

    public static RegisterReportResult Generate(RegisterReportRequest request, string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("A destination file path is required.", nameof(destinationFilePath));
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var actualFilePath = GetWritablePath(destinationFilePath);
        var identity = BuildIdentity(request);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(header => ComposeHeader(header, request, identity));
                page.Content().Element(content => ComposeContent(content, request, identity));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        document.GeneratePdf(actualFilePath);

        int pageCount;
        using (var pdf = PdfDocument.Open(actualFilePath))
        {
            pageCount = pdf.NumberOfPages;
        }

        return new RegisterReportResult(actualFilePath, pageCount);
    }

    private static void ComposeHeader(IContainer container, RegisterReportRequest request, ReportIdentity identity)
    {
        container.Padding(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(3).Text(identity.Title)
                    .FontSize(18)
                    .Bold()
                    .FontColor("#000000");

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("DrawingRegister.App.Resources.company-logo.png")
                                     ?? assembly.GetManifestResourceStream("DrawingRegister.App.Resources.WHITE LOGO RED BACKGROUND.jpg");

                    if (stream != null)
                    {
                        row.RelativeItem().AlignRight().Height(35).Image(stream).FitHeight();
                    }
                }
                catch
                {
                    // Embedded logo missing or unreadable; proceed with unbranded header.
                }
            });

            column.Item().PaddingTop(2).LineHorizontal(1).LineColor("#eb1845");

            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem(3).Column(leftCol =>
                {
                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("DISCIPLINE:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((request.Discipline ?? string.Empty).ToUpperInvariant());
                    });

                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("PROJECT NO:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((request.ProjectNumber ?? string.Empty).ToUpperInvariant());
                    });

                    leftCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("PROJECT NAME:").Bold();
                        r.RelativeItem(2).AlignLeft().Text((request.ProjectName ?? string.Empty).ToUpperInvariant());
                    });
                });

                row.RelativeItem(2).Column(rightCol =>
                {
                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("REG NO:").Bold();
                        r.RelativeItem(3).AlignLeft().Text(identity.HeaderRegisterNumber);
                    });

                    rightCol.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text("CLIENT NO:").Bold();
                        r.RelativeItem(3).AlignLeft().Text((request.ClientNumber ?? string.Empty).ToUpperInvariant());
                    });

                    if (identity.IsTransmittal && !string.IsNullOrWhiteSpace(identity.TransmittalNumber))
                    {
                        rightCol.Item().Row(r =>
                        {
                            r.RelativeItem().AlignLeft().Text("TRANSMITTAL NO:").Bold();
                            r.RelativeItem(3).AlignLeft().Text(identity.TransmittalNumber.ToUpperInvariant());
                        });
                    }
                });
            });

            column.Item().PaddingTop(2).LineHorizontal(1).LineColor("#eb1845");
        });
    }

    private static void ComposeContent(IContainer container, RegisterReportRequest request, ReportIdentity identity)
    {
        container.Column(column =>
        {
            var isTransmittal = request.Mode == RegisterReportMode.Transmittal;

            if (isTransmittal && request.SelectedIssueDate.HasValue)
            {
                var issueDate = request.SelectedIssueDate.Value;
                column.Item().PaddingBottom(10).Table(issueInfoTable =>
                {
                    issueInfoTable.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(8);
                    });

                    // Date of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .Text(string.Empty);
                    });

                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#eb1845")
                         .Padding(5)
                         .AlignCenter()
                         .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                         .AlignRight()
                         .Text("DATE OF ISSUE: " + issueDate.ToString("dd/MM/yyyy"));
                    });

                    // Distribution row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("DISTRIBUTION :").Bold());
                    });

                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff").Padding(5).Column(distributionColumn =>
                        {
                            var distributionText = string.IsNullOrWhiteSpace(request.DistributionText)
                                ? "NO RECIPIENTS SELECTED"
                                : request.DistributionText.ToUpperInvariant();

                            var distributionLines = distributionText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in distributionLines)
                            {
                                distributionColumn.Item().Text(line);
                            }
                        });
                    });

                    // Purpose of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("PURPOSE OF ISSUE :").Bold());
                    });

                    issueInfoTable.Cell().Element(c =>
                    {
                        purposeTable(c, request.PurposeOfIssue);
                    });

                    // Method of Issue row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("METHOD OF ISSUE :").Bold());
                    });

                    issueInfoTable.Cell().Element(c =>
                    {
                        methodTable(c, request.MethodOfIssue);
                    });

                    // Issued By row
                    issueInfoTable.Cell().Element(c =>
                    {
                        c.Background("#ffffff")
                         .Padding(5)
                         .AlignLeft()
                         .Text(x => x.Span("ISSUED BY :").Bold());
                    });

                    issueInfoTable.Cell().Element(c =>
                    {
                        issuedByTable(c, request.IssuedBy);
                    });
                });
            }

            // Main document table
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.5f);    // Document No
                    columns.RelativeColumn(3f);       // Description
                    columns.RelativeColumn(1f);       // Package
                    columns.RelativeColumn(0.8f);    // Type
                    columns.RelativeColumn(0.6f);    // Size
                    columns.RelativeColumn(0.8f);    // Latest Rev
                    columns.RelativeColumn(1.2f);    // Latest Date
                });

                table.Header(header =>
                {
                    void AddHeaderCell(string text)
                    {
                        header.Cell().Element(c =>
                        {
                            c.Background("#eb1845")
                             .Padding(5)
                             .AlignCenter()
                             .DefaultTextStyle(x => x.Bold().FontColor(Colors.White))
                             .Text(text);
                        });
                    }

                    AddHeaderCell("DOCUMENT NO");
                    AddHeaderCell("DESCRIPTION");
                    AddHeaderCell("PACKAGE");
                    AddHeaderCell("TYPE");
                    AddHeaderCell("SIZE");
                    AddHeaderCell("LATEST REV");
                    AddHeaderCell("LATEST DATE");
                });

                var isAlternate = false;

                if (isTransmittal)
                {
                    var documents = FilterTransmittalDocuments(request);
                    foreach (var doc in documents)
                    {
                        var latestRev = doc.RevisionHistory.OrderByDescending(r => r.Key).FirstOrDefault();
                        var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                        void AddCell(string text)
                        {
                            table.Cell().Element(c =>
                            {
                                c.Background(rowColor)
                                 .Padding(5)
                                 .AlignLeft()
                                 .AlignMiddle()
                                 .Text(text);
                            });
                        }

                        AddCell((doc.DocumentNumber ?? string.Empty).ToUpperInvariant());
                        AddCell((doc.Description ?? string.Empty).ToUpperInvariant());
                        AddCell((doc.Package ?? string.Empty).ToUpperInvariant());
                        AddCell((doc.DocumentType ?? string.Empty).ToUpperInvariant());
                        AddCell((doc.Size ?? string.Empty).ToUpperInvariant());
                        AddCell((latestRev.Value?.Revision ?? string.Empty).ToUpperInvariant());
                        AddCell(latestRev.Key != default ? latestRev.Key.ToString("yyyy-MM-dd") : string.Empty);

                        isAlternate = !isAlternate;
                    }
                }
                else
                {
                    var rows = BuildRegisterRows(request.Documents);
                    foreach (var row in rows)
                    {
                        var rowColor = isAlternate ? "#f5f5f5" : "#ffffff";

                        void AddCell(string text)
                        {
                            table.Cell().Element(c =>
                            {
                                c.Background(rowColor)
                                 .Padding(5)
                                 .AlignLeft()
                                 .AlignMiddle()
                                 .Text(text);
                            });
                        }

                        AddCell(row.DocumentNumber.ToUpperInvariant());
                        AddCell(row.Description.ToUpperInvariant());
                        AddCell(row.Package.ToUpperInvariant());
                        AddCell(row.DocumentType.ToUpperInvariant());
                        AddCell(row.Size.ToUpperInvariant());
                        AddCell(row.LatestRevision.ToUpperInvariant());
                        AddCell(row.LatestIssueDate?.ToString("dd/MM/yyyy") ?? string.Empty);

                        isAlternate = !isAlternate;
                    }
                }
            });
        });

        void purposeTable(IContainer c, string? purpose)
        {
            c.Background("#ffffff").Padding(5).Table(table =>
            {
                table.ColumnsDefinition(cols => cols.RelativeColumn());
                var text = string.IsNullOrWhiteSpace(purpose) ? "NOT SPECIFIED" : purpose.ToUpperInvariant();
                table.Cell().Element(cell => cell.Text(text));
            });
        }

        void methodTable(IContainer c, string? method)
        {
            c.Background("#ffffff").Padding(5).Table(table =>
            {
                table.ColumnsDefinition(cols => cols.RelativeColumn());
                var text = string.IsNullOrWhiteSpace(method) ? "NOT SPECIFIED" : method.ToUpperInvariant();
                table.Cell().Element(cell => cell.Text(text));
            });
        }

        void issuedByTable(IContainer c, string? issuedBy)
        {
            c.Background("#ffffff").Padding(5).Table(table =>
            {
                table.ColumnsDefinition(cols => cols.RelativeColumn());
                var text = (issuedBy ?? string.Empty).ToUpperInvariant();
                table.Cell().Element(cell => cell.AlignLeft().Text(text));
            });
        }
    }

    private static IReadOnlyList<DocumentMetadata> FilterTransmittalDocuments(RegisterReportRequest request)
    {
        if (!request.SelectedIssueDate.HasValue)
        {
            return request.Documents
                .OrderBy(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var targetDate = request.SelectedIssueDate.Value.Date;
        var query = request.Documents
            .Where(d => d.RevisionHistory.Any(r => r.Key.Date == targetDate));

        if (!string.IsNullOrWhiteSpace(request.SelectedSubfolderPath))
        {
            query = query.Where(d => d.RevisionHistory.Any(r =>
                r.Key.Date == targetDate &&
                !string.IsNullOrEmpty(r.Value.FilePath) &&
                string.Equals(Path.GetDirectoryName(r.Value.FilePath), request.SelectedSubfolderPath, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .OrderBy(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<RegisterRow> BuildRegisterRows(IEnumerable<DocumentMetadata> documents)
    {
        return documents
            .OrderBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .Select(document =>
            {
                var orderedHistory = document.RevisionHistory
                    .OrderByDescending(entry => entry.Key)
                    .ToList();

                var latestEntry = orderedHistory.FirstOrDefault(e => !e.Value.IsSuperseded);
                var latestRevision = latestEntry.Value?.Revision ?? string.Empty;
                var latestIssueDate = latestEntry.Value == null
                    ? (DateTime?)null
                    : latestEntry.Key.Date;

                return new RegisterRow(
                    document.DocumentNumber ?? string.Empty,
                    document.Description ?? string.Empty,
                    document.Package ?? string.Empty,
                    document.DocumentType ?? string.Empty,
                    document.Size ?? string.Empty,
                    latestRevision,
                    latestIssueDate);
            })
            .ToList();
    }

    private static ReportIdentity BuildIdentity(RegisterReportRequest request)
    {
        var normalizedRegisterNumber = NormalizeToken(request.RegisterNumber, "Register");

        return request.Mode switch
        {
            RegisterReportMode.DocReg => CreateDocRegIdentity(request.ProjectNumber, request.ReportDate),
            RegisterReportMode.Transmittal => new ReportIdentity(
                "TRANSMITTAL",
                normalizedRegisterNumber,
                $"Transmittal_{normalizedRegisterNumber}_{request.ReportDate:yyyyMMdd}",
                IsTransmittal: true,
                TransmittalNumber: !string.IsNullOrWhiteSpace(request.TransmittalNumber)
                    ? request.TransmittalNumber
                    : $"{normalizedRegisterNumber}-T{request.ReportDate:yyMMdd}"),
            _ => new ReportIdentity(
                "DOCUMENT AND DRAWING REGISTER",
                normalizedRegisterNumber,
                $"Register_{normalizedRegisterNumber}_{request.ReportDate:yyyyMMdd}",
                IsTransmittal: false,
                TransmittalNumber: null)
        };
    }

    private static ReportIdentity CreateDocRegIdentity(string? projectNumber, DateTime reportDate)
    {
        var docRegNumber = $"DocReg-{NormalizeToken(projectNumber, "Project")}-{reportDate:yyyyMMdd}";
        return new ReportIdentity(
            "SER DOCUMENT AND DRAWING REGISTER",
            docRegNumber,
            docRegNumber,
            IsTransmittal: false,
            TransmittalNumber: null);
    }

    private static string NormalizeToken(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string GetWritablePath(string requestedPath)
    {
        if (!IsLocked(requestedPath))
        {
            return requestedPath;
        }

        var directory = Path.GetDirectoryName(requestedPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestedPath);
        var extension = Path.GetExtension(requestedPath);

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            if (!File.Exists(candidatePath) || !IsLocked(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException($"Could not find a writable path for '{requestedPath}'.");
    }

    private static bool IsLocked(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private sealed record ReportIdentity(
        string Title,
        string HeaderRegisterNumber,
        string FileNamePrefix,
        bool IsTransmittal,
        string? TransmittalNumber);

    private sealed record RegisterRow(
        string DocumentNumber,
        string Description,
        string Package,
        string DocumentType,
        string Size,
        string LatestRevision,
        DateTime? LatestIssueDate);
}
