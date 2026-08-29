namespace Chronaiq.Domain.Enums;

/// <summary>
/// Canonical values for the <c>"BrainNodes"."SourceType"</c> column. The column is a
/// <c>VARCHAR</c> in the schema, so these are exposed as string constants rather than a
/// CLR enum — this keeps ingestion open to new source types without a migration while
/// still giving callers a typo-proof set of well-known values.
/// </summary>
public static class BrainSourceType
{
    public const string Document = "Document";
    public const string Diagram = "Diagram";
    public const string VoiceNote = "VoiceNote";
    public const string BudgetPdf = "BudgetPDF";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Document,
            Diagram,
            VoiceNote,
            BudgetPdf
        };

    public static bool IsKnown(string value) => All.Contains(value);
}
