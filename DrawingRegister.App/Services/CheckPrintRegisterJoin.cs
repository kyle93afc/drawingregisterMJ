using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

public static class CheckPrintRegisterJoin
{
    public static IReadOnlyList<CheckPrintQueueRow> BuildLiveQueue(
        IEnumerable<CheckPrint> checkPrints,
        IEnumerable<DocumentMetadata> documents)
    {
        var register = documents.ToList();

        return checkPrints.Select(checkPrint =>
        {
            var document = register.FirstOrDefault(candidate =>
                string.Equals(candidate.DocumentNumber, checkPrint.DocumentCode, StringComparison.OrdinalIgnoreCase));
            var match = document?.RevisionHistory
                .Where(entry => string.Equals(entry.Value.Revision, checkPrint.Revision, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.Key)
                .Select(entry => (KeyValuePair<DateTime, RevisionInfo>?)entry)
                .FirstOrDefault();
            var current = document?.LatestNonSupersededRevision;

            return new CheckPrintQueueRow(
                checkPrint,
                match?.Key,
                match?.Value,
                match.HasValue && current.HasValue && match.Value.Key == current.Value.Key,
                !match.HasValue
                    ? "No matching register revision"
                    : match.Value.Value.IsSuperseded
                        ? "Superseded revision"
                        : checkPrint.Status == CheckStatus.APPD
                            ? match.Value.Value.IsDistributed ? "Approved and distributed" : "Approved but not distributed"
                            : checkPrint.StatusText);
        })
        .ToList();
    }
}

public sealed record CheckPrintQueueRow(
    CheckPrint CheckPrint,
    DateTime? IssueDate,
    RevisionInfo? RegisterRevision,
    bool IsCurrent,
    string QueueReason)
{
    public string DistributionText => RegisterRevision == null
        ? string.Empty
        : RegisterRevision.IsDistributed ? "Distributed" : "Not distributed";
}
