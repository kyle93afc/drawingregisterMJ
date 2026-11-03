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
