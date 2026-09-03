using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;
using UglyToad.PdfPig;
using Xunit;

namespace DrawingRegister.App.Tests.Services;

public sealed class RegisterReportGeneratorTests : IDisposable
{
    private readonly string _tempDirectory;

    public RegisterReportGeneratorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "RegisterReportGeneratorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in test temp directory.
        }
    }

    [Fact]
    public void GetSuggestedFileName_returns_expected_naming_across_all_modes()
    {
        var reportDate = new DateTime(2026, 9, 3);

        var regRequest = CreateRequest(
            RegisterReportMode.Register,
            reportDate,
            projectNumber: "124997",
            registerNumber: "01");
        Assert.Equal("Register_01_20260903", RegisterReportGenerator.GetSuggestedFileName(regRequest));

        var transmittalRequest = CreateRequest(
            RegisterReportMode.Transmittal,
            reportDate,
            projectNumber: "124997",
            registerNumber: "01",
            selectedSubfolderName: "Planning Issue");
        Assert.Equal("Transmittal_01_20260903_Planning_Issue", RegisterReportGenerator.GetSuggestedFileName(transmittalRequest));

        var docRegRequest = CreateRequest(
            RegisterReportMode.DocReg,
            reportDate,
            projectNumber: "124997",
            registerNumber: "01");
        Assert.Equal("DocReg-124997-20260903", RegisterReportGenerator.GetSuggestedFileName(docRegRequest));
    }

    [Fact]
    public void Generate_full_register_report_creates_pdf_with_expected_layout_and_data()
    {
        var destination = Path.Combine(_tempDirectory, "full_register.pdf");
        var docs = new[]
        {
            CreateDocument("124997-002", ("A", "Tender", new DateTime(2026, 4, 1))),
            CreateDocument("124997-001", ("P01", "Information", new DateTime(2026, 3, 15)))
        };

        var request = CreateRequest(
            RegisterReportMode.Register,
            new DateTime(2026, 9, 3),
            projectNumber: "124997",
            projectName: "Highland Bridge",
            discipline: "Structural",
            registerNumber: "REG-01",
            clientNumber: "CL-900",
            documents: docs);

        var result = RegisterReportGenerator.Generate(request, destination);

        Assert.Equal(destination, result.ActualFilePath);
        Assert.True(File.Exists(destination));
        Assert.True(result.PageCount >= 1);

        using var pdf = PdfDocument.Open(destination);
        var pageText = string.Join(" ", pdf.GetPages().Select(p => p.Text));

        Assert.Contains("DOCUMENT AND DRAWING REGISTER", pageText);
        Assert.Contains("HIGHLAND BRIDGE", pageText);
        Assert.Contains("124997", pageText);
        Assert.Contains("REG-01", pageText);
        Assert.Contains("CL-900", pageText);
        Assert.Contains("124997-001", pageText);
        Assert.Contains("124997-002", pageText);
    }

    [Fact]
    public void Generate_transmittal_filters_by_issue_date_and_contains_transmittal_sections()
    {
        var destination = Path.Combine(_tempDirectory, "transmittal.pdf");
        var issueDate = new DateTime(2026, 4, 14);

        var doc1 = CreateDocument("124997-001", ("P01", "Info", new DateTime(2026, 3, 1)), ("P02", "Construction", issueDate));
        var doc2 = CreateDocument("124997-002", ("P01", "Info", new DateTime(2026, 3, 1))); // Not on issue date
        var doc3 = CreateDocument("124997-003", ("A01", "Tender", issueDate));

        var request = CreateRequest(
            RegisterReportMode.Transmittal,
            new DateTime(2026, 9, 3),
            projectNumber: "124997",
            projectName: "Office Park",
            discipline: "Civil",
            registerNumber: "02",
            documents: new[] { doc1, doc2, doc3 },
            selectedIssueDate: issueDate,
            distributionText: "ARCHITECT: ACME DESIGNS\nCONTRACTOR: BUILD CORP",
            purposeOfIssue: "For Construction",
            methodOfIssue: "Email",
            issuedBy: "KG");

        var result = RegisterReportGenerator.Generate(request, destination);

        Assert.Equal(destination, result.ActualFilePath);
        Assert.True(File.Exists(destination));

        using var pdf = PdfDocument.Open(destination);
        var pageText = string.Join(" ", pdf.GetPages().Select(p => p.Text));

        Assert.Contains("TRANSMITTAL", pageText);
        Assert.Contains("02-T260903", pageText);
        Assert.Contains("DATE OF ISSUE: 14/04/2026", pageText);
        Assert.Contains("DISTRIBUTION :", pageText);
        Assert.Contains("ARCHITECT: ACME DESIGNS", pageText);
        Assert.Contains("CONTRACTOR: BUILD CORP", pageText);
        Assert.Contains("FOR CONSTRUCTION", pageText);
        Assert.Contains("EMAIL", pageText);
        Assert.Contains("KG", pageText);

        // Contains doc1 and doc3, but doc2 was not issued on 14/04/2026
        Assert.Contains("124997-001", pageText);
        Assert.Contains("124997-003", pageText);
        Assert.DoesNotContain("124997-002", pageText);
    }

    [Fact]
    public void Generate_docreg_uses_ser_header_and_naming()
    {
        var destination = Path.Combine(_tempDirectory, "docreg.pdf");
        var docs = new[] { CreateDocument("DR-100", ("1", "Warrant", new DateTime(2026, 4, 1))) };

        var request = CreateRequest(
            RegisterReportMode.DocReg,
            new DateTime(2026, 9, 3),
            projectNumber: "124997",
            documents: docs);

        var result = RegisterReportGenerator.Generate(request, destination);

        Assert.Equal(destination, result.ActualFilePath);
        using var pdf = PdfDocument.Open(destination);
        var pageText = string.Join(" ", pdf.GetPages().Select(p => p.Text));

        Assert.Contains("SER DOCUMENT AND DRAWING REGISTER", pageText);
        Assert.Contains("DocReg-124997-20260903", pageText);
        Assert.Contains("DR-100", pageText);
    }

    [Fact]
    public void Generate_latest_non_superseded_revision_rules_match_specification()
    {
        var destination = Path.Combine(_tempDirectory, "superseded_rules.pdf");

        // Document with superseded newest revision
        var docWithSuperseded = CreateDocument("DR-SUPER", ("1", "Old", new DateTime(2026, 1, 1)));
        docWithSuperseded.RevisionHistory[new DateTime(2026, 4, 1)] = new RevisionInfo
        {
            Revision = "2-SUPERSEDED",
            Purpose = "Withdrawn",
            IsSuperseded = true
        };

        // Document with reissued revision
        var docReissued = CreateDocument("DR-REISSUE",
            ("C01", "Info", new DateTime(2026, 2, 1)),
            ("C01", "Construction", new DateTime(2026, 4, 14)));

        // Document with placeholder revision
        var docPlaceholder = CreateDocument("DR-PLACEHOLDER", ("-", "Preliminary", new DateTime(2026, 4, 1)));

        var request = CreateRequest(
            RegisterReportMode.Register,
            new DateTime(2026, 9, 3),
            documents: new[] { docWithSuperseded, docReissued, docPlaceholder });

        var result = RegisterReportGenerator.Generate(request, destination);

        using var pdf = PdfDocument.Open(destination);
        var pageText = string.Join(" ", pdf.GetPages().Select(p => p.Text));

        // Latest non-superseded for DR-SUPER is 1, not 2-SUPERSEDED
        Assert.Contains("DR-SUPER", pageText);
        Assert.DoesNotContain("2-SUPERSEDED", pageText);

        Assert.Contains("DR-REISSUE", pageText);
        Assert.Contains("C01", pageText);
        Assert.Contains("14/04/2026", pageText);

        Assert.Contains("DR-PLACEHOLDER", pageText);
    }

    [Fact]
    public void Generate_when_destination_path_is_locked_saves_to_suffixed_filename()
    {
        var requestedPath = Path.Combine(_tempDirectory, "locked_target.pdf");

        // Create and hold an exclusive lock on requestedPath
        using (var lockStream = new FileStream(requestedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var request = CreateRequest(
                RegisterReportMode.Register,
                new DateTime(2026, 9, 3),
                documents: new[] { CreateDocument("DR-001", ("P1", "Draft", DateTime.Today)) });

            var result = RegisterReportGenerator.Generate(request, requestedPath);

            var expectedSuffixPath = Path.Combine(_tempDirectory, "locked_target (1).pdf");
            Assert.Equal(expectedSuffixPath, result.ActualFilePath);
            Assert.True(File.Exists(expectedSuffixPath));
        }
    }

    [Fact]
    public void Generate_with_empty_documents_and_null_metadata_does_not_throw()
    {
        var destination = Path.Combine(_tempDirectory, "empty.pdf");
        var request = new RegisterReportRequest(
            Mode: RegisterReportMode.Register,
            ReportDate: new DateTime(2026, 9, 3),
            ProjectNumber: null,
            ProjectName: null,
            Discipline: null,
            RegisterNumber: null,
            ClientNumber: null,
            Documents: Array.Empty<DocumentMetadata>());

        var result = RegisterReportGenerator.Generate(request, destination);

        Assert.True(File.Exists(result.ActualFilePath));
        Assert.True(result.PageCount >= 1);
    }

    private static RegisterReportRequest CreateRequest(
        RegisterReportMode mode,
        DateTime reportDate,
        string? projectNumber = "124997",
        string? projectName = "Test Project",
        string? discipline = "Structural",
        string? registerNumber = "01",
        string? clientNumber = "C100",
        IReadOnlyList<DocumentMetadata>? documents = null,
        DateTime? selectedIssueDate = null,
        string? selectedSubfolderName = null,
        string? distributionText = null,
        string? purposeOfIssue = null,
        string? methodOfIssue = null,
        string? issuedBy = null)
    {
        return new RegisterReportRequest(
            Mode: mode,
            ReportDate: reportDate,
            ProjectNumber: projectNumber,
            ProjectName: projectName,
            Discipline: discipline,
            RegisterNumber: registerNumber,
            ClientNumber: clientNumber,
            Documents: documents ?? Array.Empty<DocumentMetadata>(),
            SelectedIssueDate: selectedIssueDate,
            SelectedSubfolderName: selectedSubfolderName,
            DistributionText: distributionText,
            PurposeOfIssue: purposeOfIssue,
            MethodOfIssue: methodOfIssue,
            IssuedBy: issuedBy);
    }

    private static DocumentMetadata CreateDocument(string documentNumber, params (string Revision, string Purpose, DateTime Date)[] history)
    {
        var document = new DocumentMetadata
        {
            DocumentNumber = documentNumber,
            Description = "Drawing Description " + documentNumber,
            Package = "01",
            DocumentType = "DR",
            Size = "A1",
            FilePath = $@"C:\Project\latest\{documentNumber}.pdf"
        };

        foreach (var entry in history)
        {
            document.RevisionHistory[entry.Date] = new RevisionInfo
            {
                Revision = entry.Revision,
                Purpose = entry.Purpose,
                FilePath = $@"C:\Project\{entry.Date:yyyyMMdd}\{documentNumber}.pdf"
            };
        }

        return document;
    }
}
