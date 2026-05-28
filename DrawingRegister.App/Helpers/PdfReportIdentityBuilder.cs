namespace DrawingRegister.App.Helpers;

internal enum PdfReportMode
{
    Register,
    Transmittal,
    DocReg
}

internal sealed record PdfReportIdentity(
    string Title,
    string HeaderRegisterNumber,
    string FileNamePrefix,
    bool IsTransmittal,
    string? TransmittalNumber);

internal static class PdfReportIdentityBuilder
{
    internal static PdfReportIdentity Create(
        PdfReportMode mode,
        string? projectNumber,
        string? registerNumber,
        DateTime reportDate)
    {
        var normalizedRegisterNumber = NormalizeToken(registerNumber, "Register");

        return mode switch
        {
            PdfReportMode.DocReg => CreateDocReg(projectNumber, reportDate),
            PdfReportMode.Transmittal => new PdfReportIdentity(
                "TRANSMITTAL",
                normalizedRegisterNumber,
                $"Transmittal_{normalizedRegisterNumber}_{reportDate:yyyyMMdd}",
                IsTransmittal: true,
                TransmittalNumber: $"{normalizedRegisterNumber}-T{reportDate:yyMMdd}"),
            _ => new PdfReportIdentity(
                "DOCUMENT AND DRAWING REGISTER",
                normalizedRegisterNumber,
                $"Register_{normalizedRegisterNumber}_{reportDate:yyyyMMdd}",
                IsTransmittal: false,
                TransmittalNumber: null)
        };
    }

    private static PdfReportIdentity CreateDocReg(string? projectNumber, DateTime reportDate)
    {
        var docRegNumber = $"DocReg-{NormalizeToken(projectNumber, "Project")}-{reportDate:yyyyMMdd}";
        return new PdfReportIdentity(
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
}
