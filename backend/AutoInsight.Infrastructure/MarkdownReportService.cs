using System.Text;
using System.Globalization;
using AutoInsight.Core;

namespace AutoInsight.Infrastructure;

public sealed class MarkdownReportService(IDataProfiler profiler) : IReportService
{
    public string GenerateMarkdown(Dataset dataset)
    {
        var overview = profiler.GetOverview(dataset, 0);
        var columns = profiler.GetColumnProfiles(dataset);
        var quality = profiler.GetQualityFindings(dataset);
        var builder = new StringBuilder($"# AutoInsight report: {Clean(dataset.FileName)}\n\n");
        builder.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC  ");
        builder.AppendLine("Source: synthetic or user-supplied dataset analyzed locally.\n");
        builder.AppendLine("## Dataset overview\n");
        builder.AppendLine($"- Rows: {overview.RowCount:N0}\n- Columns: {overview.ColumnCount}\n- Missing values: {overview.TotalMissingValues:N0}\n- Duplicate rows: {overview.DuplicateRows:N0}\n");
        builder.AppendLine("## Column profiles\n");
        builder.AppendLine("| Column | Type | Missing | Unique | Mean | Median |\n|---|---:|---:|---:|---:|---:|");
        foreach (var c in columns) builder.AppendLine($"| {Clean(c.Name)} | {c.DataType} | {c.MissingCount} | {c.UniqueCount} | {c.Statistics?.Mean.ToString("F2", CultureInfo.InvariantCulture) ?? "—"} | {c.Statistics?.Median.ToString("F2", CultureInfo.InvariantCulture) ?? "—"} |");
        builder.AppendLine("\n## Data quality observations\n");
        if (quality.Count == 0) builder.AppendLine("No rule-based quality findings were detected.");
        else foreach (var finding in quality) builder.AppendLine($"- **{Clean(finding.Title)}{(finding.Column is null ? "" : $" — {Clean(finding.Column)}")}**: {Clean(finding.Explanation)} Recommendation: {Clean(finding.Recommendation)}");
        builder.AppendLine("\n## Limitations\n\nType inference and outlier detection are heuristic. Findings require domain review. The original dataset was not changed. This report contains calculated results, not an LLM interpretation.");
        return builder.ToString();
    }

    private static string Clean(string value)
    {
        var singleLine = new string(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
        return singleLine
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }
}
