using System;
using System.IO;
using DrawingRegister.App.Models;
using DrawingRegister.App.Services;

namespace DrawingRegister.App.Tests.Services;

public sealed class DocRegFolderExportTests
{
    [Fact]
    public void Validate_requires_project_number_included_rows_and_document_numbers()
    {
        var missingProject = DocRegFolderExport.Validate(
            new[] { CreateDrawing("A-101", include: true) },
            projectNumber: "  ");
        Assert.Contains(missingProject, message => message.Contains("Project number", StringComparison.OrdinalIgnoreCase));

        var noneIncluded = DocRegFolderExport.Validate(
            new[] { CreateDrawing("A-101", include: false) },
            projectNumber: "12499");
        Assert.Contains(noneIncluded, message => message.Contains("at least one", StringComparison.OrdinalIgnoreCase));

        var blankNumber = DocRegFolderExport.Validate(
            new[] { CreateDrawing("  ", include: true) },
            projectNumber: "12499");
        Assert.Contains(blankNumber, message => message.Contains("document number", StringComparison.OrdinalIgnoreCase));

        var ok = DocRegFolderExport.Validate(
            new[] { CreateDrawing("A-101", include: true) },
            projectNumber: "12499");
        Assert.Empty(ok);
    }

    [Fact]
    public void BuildRequest_uses_docreg_mode_and_does_not_carry_register_documents()
    {
        var drawing = CreateDrawing("A-101", include: true);

        var request = DocRegFolderExport.BuildRequest(
            reportDate: new DateTime(2026, 11, 3),
            projectNumber: "12499",
            projectName: "Sample Works",
            discipline: "Structural",
            registerNumber: "01",
            clientNumber: "C100",
            organization: OrganizationRegistry.MJ,
            drawings: new[] { drawing });

        Assert.Equal(RegisterReportMode.DocReg, request.Mode);
        Assert.Empty(request.Documents);
        Assert.Equal(new[] { drawing }, request.DocRegDrawings);
        Assert.Equal("12499", request.ProjectNumber);
        Assert.Equal("Sample Works", request.ProjectName);
    }

    [Fact]
    public void WouldOverwriteSourcePdf_is_true_only_for_scanned_drawing_paths()
    {
        var source = CreateDrawing("A-101", include: true);

        Assert.True(DocRegFolderExport.WouldOverwriteSourcePdf(source.FilePath, new[] { source }));
        Assert.False(DocRegFolderExport.WouldOverwriteSourcePdf(
            Path.Combine(Path.GetDirectoryName(source.FilePath)!, "DocReg-12499-20261103.pdf"),
            new[] { source }));
    }

    private static DocRegDrawing CreateDrawing(string documentNumber, bool include)
    {
        return new DocRegDrawing
        {
            Include = include,
            Section = DocRegSection.ArchitectExternal,
            DocumentNumber = documentNumber,
            Description = "Ground Floor Plan",
            Revision = "B",
            FilePath = Path.Combine(Path.GetTempPath(), "docreg-export-tests", documentNumber.Trim() + ".pdf"),
            FileName = documentNumber.Trim() + ".pdf",
            ParseNote = string.Empty
        };
    }
}
