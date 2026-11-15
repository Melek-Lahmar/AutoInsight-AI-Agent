using System.Text.Json.Serialization;

namespace AutoInsight.Core;

public enum DataKind { Numeric, Date, Categorical, Text }

public sealed record Dataset(
    Guid Id,
    string FileName,
    DateTimeOffset UploadedAt,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    char Delimiter);

public sealed record DatasetSummary(Guid Id, string FileName, int RowCount, int ColumnCount, DateTimeOffset UploadedAt);

public sealed record DatasetOverview(
    Guid Id,
    string FileName,
    int RowCount,
    int ColumnCount,
    int DuplicateRows,
    int TotalMissingValues,
    char Delimiter,
    DateTimeOffset UploadedAt,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Preview);

public sealed record ColumnProfile(
    string Name,
    DataKind DataType,
    int MissingCount,
    int UniqueCount,
    double MissingPercentage,
    NumericStatistics? Statistics,
    IReadOnlyList<CategoryFrequency>? Frequencies);

public sealed record NumericStatistics(
    int Count,
    double Minimum,
    double Maximum,
    double Mean,
    double Median,
    double StandardDeviation,
    double FirstQuartile,
    double ThirdQuartile);

public sealed record CategoryFrequency(string Value, int Count, double Percentage);

public sealed record QualityFinding(
    string Code,
    string Severity,
    string Title,
    string Explanation,
    string? Column,
    int Count,
    string Recommendation);

public sealed record DataPoint(string Label, double Value, double? SecondaryValue = null);

public sealed record ChartSpecification(
    string Type,
    string Title,
    string XAxis,
    string YAxis,
    IReadOnlyList<DataPoint> Data,
    string? Series = null);

public sealed record UploadResult(DatasetOverview Overview, IReadOnlyList<ColumnProfile> Columns);

public sealed record AgentRequest(Guid DatasetId, string Message, string Mode = "demo");

public sealed record ToolExecution(string Tool, string Summary, int DurationMs);

public sealed record AgentResponse(
    string Mode,
    string Message,
    IReadOnlyList<ToolExecution> ToolExecutions,
    ChartSpecification? Chart,
    bool UsedExternalProvider);

public sealed record VisualizationRequest(string Type, string XColumn, string? YColumn = null, int Bins = 10);

public sealed record ReportRequest(Guid DatasetId);

public sealed class DatasetNotFoundException(Guid id) : Exception($"Dataset '{id}' was not found.");
public sealed class DatasetValidationException(string message) : Exception(message);

public sealed class AiOptions
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    [JsonIgnore] public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxToolInvocations { get; set; } = 8;
    public bool IsConfigured => IsSupportedProvider(Provider)
        && !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(ApiKey);

    public static bool IsSupportedProvider(string? provider) => provider is not null &&
        (provider.Equals("openai", StringComparison.OrdinalIgnoreCase)
         || provider.Equals("google", StringComparison.OrdinalIgnoreCase)
         || provider.Equals("googleai", StringComparison.OrdinalIgnoreCase)
         || provider.Equals("gemini", StringComparison.OrdinalIgnoreCase));
}
