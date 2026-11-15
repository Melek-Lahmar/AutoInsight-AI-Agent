using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using AutoInsight.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AutoInsight.Infrastructure;

public sealed class DatasetAnalysisPlugin(
    IDatasetStore store,
    IDataProfiler profiler,
    ILogger<DatasetAnalysisPlugin> logger,
    int maxInvocations = 8)
{
    private readonly List<ToolExecution> _executions = [];
    private int _invocations;
    public IReadOnlyList<ToolExecution> Executions => _executions;
    public ChartSpecification? LastChart { get; private set; }

    [KernelFunction("get_dataset_overview")]
    [Description("Returns row count, column count, missing values, duplicates, and delimiter without raw records.")]
    public string GetDatasetOverview([Description("Dataset UUID supplied by the application")] string datasetId) => Run("get_dataset_overview", () =>
    {
        var overview = profiler.GetOverview(GetDataset(datasetId), 0);
        return JsonSerializer.Serialize(new
        {
            overview.Id, overview.RowCount, overview.ColumnCount,
            overview.DuplicateRows, overview.TotalMissingValues, overview.Delimiter
        });
    }, "Calculated dataset dimensions and completeness without exposing raw rows.");

    [KernelFunction("get_column_information")]
    [Description("Returns inferred data types, missing and unique counts, and numeric statistics for every column without raw values.")]
    public string GetColumnInformation([Description("Dataset UUID supplied by the application")] string datasetId) => Run("get_column_information", () =>
        JsonSerializer.Serialize(profiler.GetColumnProfiles(GetDataset(datasetId)).Select(profile => new
        {
            profile.Name, profile.DataType, profile.MissingCount, profile.UniqueCount,
            profile.MissingPercentage, profile.Statistics
        })), "Profiled every column without transmitting raw values.");

    [KernelFunction("get_missing_values")]
    [Description("Returns deterministic missing-value counts for each column.")]
    public string GetMissingValues([Description("Dataset UUID supplied by the application")] string datasetId) => Run("get_missing_values", () =>
    {
        var result = profiler.GetColumnProfiles(GetDataset(datasetId)).Select(p => new { column = p.Name, missing = p.MissingCount, percentage = p.MissingPercentage });
        return JsonSerializer.Serialize(result);
    }, "Counted missing values by column.");

    [KernelFunction("get_duplicate_rows")]
    [Description("Returns the number of full-row duplicates.")]
    public string GetDuplicateRows([Description("Dataset UUID supplied by the application")] string datasetId) => Run("get_duplicate_rows", () =>
        JsonSerializer.Serialize(new { duplicateRows = profiler.GetOverview(GetDataset(datasetId), 0).DuplicateRows }), "Checked complete rows for duplicates.");

    [KernelFunction("calculate_statistics")]
    [Description("Returns deterministic descriptive statistics for numeric columns. Use columnName to select one column, or omit it for all numeric columns.")]
    public string CalculateStatistics(
        [Description("Dataset UUID supplied by the application")] string datasetId,
        [Description("Optional exact column name")] string? columnName = null) => Run("calculate_statistics", () =>
    {
        var profiles = profiler.GetColumnProfiles(GetDataset(datasetId)).Where(p => p.Statistics is not null);
        if (!string.IsNullOrWhiteSpace(columnName)) profiles = profiles.Where(p => p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(profiles.Select(p => new { column = p.Name, statistics = p.Statistics }));
    }, string.IsNullOrWhiteSpace(columnName) ? "Calculated all numeric statistics." : $"Calculated statistics for {columnName}.");

    [KernelFunction("get_categorical_distribution")]
    [Description("Returns value frequencies for categorical columns.")]
    public string GetCategoricalDistribution(
        [Description("Dataset UUID supplied by the application")] string datasetId,
        [Description("Optional exact column name")] string? columnName = null) => Run("get_categorical_distribution", () =>
    {
        var dataset = GetDataset(datasetId);
        var profiles = profiler.GetColumnProfiles(dataset)
            .Where(p => p.Frequencies is not null
                && p.UniqueCount <= 30
                && p.UniqueCount < dataset.Rows.Count
                && p.UniqueCount <= Math.Max(2, dataset.Rows.Count / 2));
        if (!string.IsNullOrWhiteSpace(columnName)) profiles = profiles.Where(p => p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(profiles.Select(p => new { column = p.Name, frequencies = p.Frequencies }));
    }, "Calculated bounded low-cardinality frequencies; identifier-like columns were excluded.");

    [KernelFunction("detect_outliers")]
    [Description("Returns local data quality findings including potential IQR outliers; it never modifies the dataset.")]
    public string DetectOutliers([Description("Dataset UUID supplied by the application")] string datasetId) => Run("detect_outliers", () =>
        JsonSerializer.Serialize(profiler.GetQualityFindings(GetDataset(datasetId)).Where(f => f.Code == "potential_outliers")), "Applied the 1.5 × IQR outlier rule.");

    [KernelFunction("get_data_quality")]
    [Description("Returns all deterministic data quality findings with counts, explanations, and non-destructive recommendations.")]
    public string GetDataQuality([Description("Dataset UUID supplied by the application")] string datasetId) => Run("get_data_quality", () =>
        JsonSerializer.Serialize(profiler.GetQualityFindings(GetDataset(datasetId))), "Ran the complete data quality assessment.");

    [KernelFunction("generate_chart_specification")]
    [Description("Generates safe chart data. Supported types: bar, categorical, histogram, line, scatter, missing.")]
    public string GenerateChartSpecification(
        [Description("Dataset UUID supplied by the application")] string datasetId,
        [Description("Chart type")] string chartType,
        [Description("X-axis column; use any valid column for a missing-values chart")] string xColumn,
        [Description("Y-axis column for line or scatter charts")] string? yColumn = null) => Run("generate_chart_specification", () =>
    {
        LastChart = profiler.GenerateChart(GetDataset(datasetId), new(chartType, xColumn, yColumn));
        return JsonSerializer.Serialize(new
        {
            LastChart.Type, LastChart.Title, LastChart.XAxis, LastChart.YAxis,
            pointCount = LastChart.Data.Count, chartPreparedForLocalRendering = true
        });
    }, $"Generated a safe {chartType} chart specification.");

    private Dataset GetDataset(string id) => Guid.TryParse(id, out var parsed) ? store.Get(parsed) : throw new DatasetValidationException("The dataset ID is invalid.");

    private string Run(string name, Func<string> action, string summary)
    {
        if (Interlocked.Increment(ref _invocations) > maxInvocations) throw new InvalidOperationException($"The maximum of {maxInvocations} tool calls was reached.");
        var stopwatch = Stopwatch.StartNew();
        try { return action(); }
        finally
        {
            stopwatch.Stop();
            _executions.Add(new(name, summary, (int)stopwatch.ElapsedMilliseconds));
            logger.LogInformation("Agent tool {ToolName} completed in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
        }
    }
}
