using System.Text.Json.Serialization;

namespace DrawingRegister.App.Models;

public enum CheckStatus
{
    FC,
    AWC,
    APPD,
    UNKNOWN,
    CONFLICT,
    COMMENTS // appended: Status is persisted as an int in project_data.json
}

public sealed class CheckPrint
{
    public string DocumentCode { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public int Cp { get; set; }
    public CheckStatus? Status { get; set; }
    public bool BackDrafted { get; set; }
    public string StampAuthor { get; set; } = string.Empty;
    public DateTime? StampDate { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsFlagged => !string.IsNullOrEmpty(Issue);

    [JsonIgnore]
    public string StatusText => Status switch
    {
        CheckStatus.FC => "FC — no stamp annotation found",
        CheckStatus.AWC => "AWC — approved with comments",
        CheckStatus.APPD => "APPD — approved",
        CheckStatus.COMMENTS => "COMMENTS — checked, technician action required",
        CheckStatus.UNKNOWN => "UNKNOWN — review required",
        CheckStatus.CONFLICT => "CONFLICT — review required",
        _ => "Flagged"
    };

    [JsonIgnore]
    public string StampDateText => StampDate?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? string.Empty;
}

public sealed record CheckPlan(IReadOnlyList<CheckPrint> Entries);

public sealed record ApplyResult(IReadOnlyList<CheckPrint> Facts);
