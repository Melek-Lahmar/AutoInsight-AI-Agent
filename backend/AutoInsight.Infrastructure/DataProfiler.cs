using System.Globalization;
using System.Text.Json;
using AutoInsight.Core;

namespace AutoInsight.Infrastructure;

public sealed class DataProfiler : IDataProfiler
{
    public DatasetOverview GetOverview(Dataset dataset, int previewRows = 12)
    {
        var missing = dataset.Rows.Sum(row => dataset.Headers.Count(h => string.IsNullOrWhiteSpace(row[h])));
        return new DatasetOverview(dataset.Id, dataset.FileName, dataset.Rows.Count, dataset.Headers.Count,
            CountDuplicates(dataset), missing, dataset.Delimiter, dataset.UploadedAt, dataset.Rows.Take(previewRows).ToArray());
    }

    public IReadOnlyList<ColumnProfile> GetColumnProfiles(Dataset dataset) => dataset.Headers.Select(header =>
    {
        var all = dataset.Rows.Select(r => r[header]).ToArray();
        var values = all.Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>().ToArray();
        var kind = DetectKind(values);
        var missing = all.Length - values.Length;
        var unique = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        NumericStatistics? stats = null;
        IReadOnlyList<CategoryFrequency>? frequencies = null;
        if (kind == DataKind.Numeric && values.Length > 0) stats = CalculateStatistics(values.Select(ParseNumber).ToArray());
        if (kind == DataKind.Categorical || (kind == DataKind.Text && unique <= 30)) frequencies = GetFrequencies(values);
        return new ColumnProfile(header, kind, missing, unique, all.Length == 0 ? 0 : Math.Round(missing * 100d / all.Length, 2), stats, frequencies);
    }).ToArray();

    public IReadOnlyList<QualityFinding> GetQualityFindings(Dataset dataset)
    {
        var findings = new List<QualityFinding>();
        var duplicateCount = CountDuplicates(dataset);
        if (duplicateCount > 0) findings.Add(new("duplicate_rows", "warning", "Duplicate rows",
            $"{duplicateCount} row(s) repeat a complete record.", null, duplicateCount, "Review duplicates before removing them; they may be legitimate repeated events."));

        foreach (var header in dataset.Headers)
        {
            var raw = dataset.Rows.Select(r => r[header]).ToArray();
            var values = raw.Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>().ToArray();
            var missing = raw.Length - values.Length;
            if (missing > 0) findings.Add(new("missing_values", missing > raw.Length * .2 ? "high" : "warning", "Missing values",
                $"{missing} of {raw.Length} values are missing ({(raw.Length == 0 ? 0 : missing * 100d / raw.Length):F1}%).", header, missing,
                "Choose an explicit imputation or exclusion rule based on the meaning of this field."));
            var unique = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (values.Length > 0 && unique == 1) findings.Add(new("constant_column", "info", "Constant column",
                "Every observed value is identical, so this column carries no variance.", header, values.Length, "Exclude it from correlation or predictive analysis."));

            var numeric = values.Count(IsNumber);
            if (numeric > 0 && numeric < values.Length)
                findings.Add(new("mixed_numeric", "warning", "Inconsistent numeric values",
                    $"{values.Length - numeric} value(s) are non-numeric while {numeric} are numeric.", header, values.Length - numeric, "Correct formatting or keep the column as text."));

            if (numeric == values.Length && numeric >= 4)
            {
                var numbers = values.Select(ParseNumber).Order().ToArray();
                var q1 = Percentile(numbers, .25); var q3 = Percentile(numbers, .75); var iqr = q3 - q1;
                var outliers = numbers.Count(n => n < q1 - 1.5 * iqr || n > q3 + 1.5 * iqr);
                if (outliers > 0) findings.Add(new("potential_outliers", "info", "Potential numerical outliers",
                    $"{outliers} value(s) fall outside the 1.5 × IQR fences.", header, outliers, "Inspect these records in context; do not remove them automatically."));
            }

            if (values.Length >= 5 && unique > 1 && unique <= 30)
            {
                var top = values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).Max(g => g.Count());
                if (top >= values.Length * .9) findings.Add(new("category_imbalance", "info", "Highly concentrated category",
                    $"The most common category represents {top * 100d / values.Length:F1}% of observed values.", header, top, "Account for this imbalance when interpreting comparisons."));
            }
        }

        return findings;
    }

    public ChartSpecification GenerateChart(Dataset dataset, VisualizationRequest request)
    {
        var x = ResolveHeader(dataset, request.XColumn);
        var type = request.Type.ToLowerInvariant();
        if (type is "bar" or "categorical")
        {
            var points = dataset.Rows.Select(r => r[x]).Where(v => v is not null).Cast<string>()
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).Take(20)
                .Select(g => new DataPoint(g.Key, g.Count())).ToArray();
            return new("bar", $"Distribution of {x}", x, "Count", points);
        }
        if (type == "missing")
        {
            var points = dataset.Headers.Select(h => new DataPoint(h, dataset.Rows.Count(r => string.IsNullOrWhiteSpace(r[h])))).ToArray();
            return new("bar", "Missing values by column", "Column", "Missing values", points);
        }
        if (type == "histogram")
        {
            var numbers = GetNumbers(dataset, x);
            if (numbers.Length == 0) throw new DatasetValidationException($"Column '{x}' has no numeric values.");
            var bins = Math.Clamp(request.Bins, 3, 30); var min = numbers.Min(); var max = numbers.Max();
            var width = max == min ? 1 : (max - min) / bins;
            var counts = new int[bins];
            foreach (var n in numbers) counts[Math.Min(bins - 1, (int)((n - min) / width))]++;
            var points = counts.Select((count, i) => new DataPoint($"{min + i * width:F1}–{min + (i + 1) * width:F1}", count)).ToArray();
            return new("bar", $"Histogram of {x}", x, "Frequency", points);
        }
        if (type is "scatter" or "line")
        {
            if (string.IsNullOrWhiteSpace(request.YColumn)) throw new DatasetValidationException("A Y column is required for this chart type.");
            var y = ResolveHeader(dataset, request.YColumn);
            var points = dataset.Rows.Select(r => new { Label = r[x], Y = r[y] })
                .Where(p => p.Label is not null && p.Y is not null && IsNumber(p.Y))
                .Take(500).Select(p => new DataPoint(p.Label!, ParseNumber(p.Y!))).ToArray();
            return new(type, $"{y} by {x}", x, y, points, y);
        }
        throw new DatasetValidationException("Supported chart types are bar, categorical, histogram, line, scatter, and missing.");
    }

    public static DataKind DetectKind(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return DataKind.Text;
        if (values.All(IsNumber)) return DataKind.Numeric;
        if (values.All(v => DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _))) return DataKind.Date;
        var unique = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return unique <= Math.Max(20, Math.Ceiling(values.Count * .2)) ? DataKind.Categorical : DataKind.Text;
    }

    public static NumericStatistics CalculateStatistics(double[] values)
    {
        if (values.Length == 0) throw new ArgumentException("At least one numeric value is required.", nameof(values));
        Array.Sort(values); var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
        return new(values.Length, values[0], values[^1], mean, Percentile(values, .5), Math.Sqrt(variance), Percentile(values, .25), Percentile(values, .75));
    }

    private static IReadOnlyList<CategoryFrequency> GetFrequencies(string[] values) => values
        .GroupBy(v => v, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).Take(20)
        .Select(g => new CategoryFrequency(g.Key, g.Count(), Math.Round(g.Count() * 100d / values.Length, 2))).ToArray();
    private static int CountDuplicates(Dataset dataset) => dataset.Rows
        .GroupBy(r => JsonSerializer.Serialize(dataset.Headers.Select(h => r[h]).ToArray()), StringComparer.Ordinal)
        .Sum(g => Math.Max(0, g.Count() - 1));
    private static string ResolveHeader(Dataset dataset, string requested) => dataset.Headers.FirstOrDefault(h => h.Equals(requested, StringComparison.OrdinalIgnoreCase)) ?? throw new DatasetValidationException($"Column '{requested}' was not found.");
    private static double[] GetNumbers(Dataset d, string h) => d.Rows.Select(r => r[h]).Where(v => IsNumber(v)).Select(v => ParseNumber(v!)).ToArray();
    private static bool IsNumber(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number);
    private static double ParseNumber(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1) return sorted[0];
        var position = (sorted.Length - 1) * percentile; var lower = (int)Math.Floor(position); var upper = (int)Math.Ceiling(position);
        return lower == upper ? sorted[lower] : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
