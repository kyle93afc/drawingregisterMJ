using DrawingRegister.App.Converters;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Tests.Converters;

public sealed class LatestRevisionConverterTests
{
    [Fact]
    public void Convert_returns_revision_from_latest_history_entry_even_when_same_day_has_higher_revision()
    {
        var converter = new LatestRevisionConverter();
        var revisionHistory = new Dictionary<DateTime, RevisionInfo>
        {
            [new DateTime(2026, 4, 14, 8, 0, 0)] = new() { Revision = "3" },
            [new DateTime(2026, 4, 14, 16, 0, 0)] = new() { Revision = "1" }
        };

        var result = converter.Convert(revisionHistory, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("1", result);
    }
}
