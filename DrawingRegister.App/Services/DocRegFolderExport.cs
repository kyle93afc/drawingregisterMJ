using System.IO;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public static class DocRegFolderExport
{
    public static IReadOnlyList<string> Validate(IEnumerable<DocRegDrawing> drawings, string? projectNumber)
    {
        ArgumentNullException.ThrowIfNull(drawings);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(projectNumber))
            errors.Add("Project number is required.");

        var included = drawings.Where(drawing => drawing.Include).ToList();
        if (included.Count == 0)
            errors.Add("Select at least one drawing.");
        if (included.Any(drawing => string.IsNullOrWhiteSpace(drawing.DocumentNumber)))
            errors.Add("Included rows need a document number.");

        return errors;
    }

    public static bool WouldOverwriteSourcePdf(string destinationPath, IEnumerable<DocRegDrawing> drawings)
    {
        ArgumentNullException.ThrowIfNull(drawings);
        if (string.IsNullOrWhiteSpace(destinationPath))
            return false;

        var destination = Path.GetFullPath(destinationPath);
        return drawings.Any(drawing =>
            !string.IsNullOrWhiteSpace(drawing.FilePath) &&
            string.Equals(Path.GetFullPath(drawing.FilePath), destination, StringComparison.OrdinalIgnoreCase));
    }

    public static RegisterReportRequest BuildRequest(
        DateTime reportDate,
        string projectNumber,
        string? projectName,
        string? discipline,
        string? registerNumber,
        string? clientNumber,
        OrganizationProfile? organization,
        IReadOnlyList<DocRegDrawing> drawings)
    {
        ArgumentNullException.ThrowIfNull(drawings);

        return new RegisterReportRequest(
            Mode: RegisterReportMode.DocReg,
            ReportDate: reportDate,
            ProjectNumber: projectNumber,
            ProjectName: projectName,
            Discipline: discipline,
            RegisterNumber: registerNumber,
            ClientNumber: clientNumber,
            Documents: Array.Empty<DocumentMetadata>(),
            Organization: organization,
            DocRegDrawings: drawings);
    }
}
