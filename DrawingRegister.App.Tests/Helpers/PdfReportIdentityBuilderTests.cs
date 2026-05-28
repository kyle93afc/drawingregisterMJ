using DrawingRegister.App.Helpers;

namespace DrawingRegister.App.Tests.Helpers;

public sealed class PdfReportIdentityBuilderTests
{
    [Fact]
    public void Create_returns_docreg_identity_with_ser_title_and_selected_date()
    {
        var identity = PdfReportIdentityBuilder.Create(
            PdfReportMode.DocReg,
            projectNumber: "66012",
            registerNumber: "66012-M+J-00-XX-RE-S-00-01",
            reportDate: new DateTime(2026, 5, 28));

        Assert.Equal("SER DOCUMENT AND DRAWING REGISTER", identity.Title);
        Assert.Equal("DocReg-66012-20260528", identity.HeaderRegisterNumber);
        Assert.Equal("DocReg-66012-20260528", identity.FileNamePrefix);
        Assert.False(identity.IsTransmittal);
    }
}
