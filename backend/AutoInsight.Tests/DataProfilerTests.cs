using AutoInsight.Core;
using AutoInsight.Infrastructure;

namespace AutoInsight.Tests;

public sealed class DataProfilerTests
{
    private readonly DataProfiler _profiler = new();

    [Fact]
    public void Calculates_types_statistics_missing_and_duplicates()
    {
        var dataset = DatasetWithRows(
            new() { ["Age"] = "10", ["Group"] = "A" },
            new() { ["Age"] = "20", ["Group"] = "A" },
            new() { ["Age"] = null, ["Group"] = "B" },
            new() { ["Age"] = "20", ["Group"] = "A" });

        var columns = _profiler.GetColumnProfiles(dataset);
        var age = Assert.Single(columns, c => c.Name == "Age");
        Assert.Equal(DataKind.Numeric, age.DataType);
        Assert.Equal(1, age.MissingCount);
        Assert.Equal(50d / 3d, age.Statistics!.Mean, 8);
        Assert.Equal(20, age.Statistics.Median);
        Assert.Equal(1, _profiler.GetOverview(dataset).DuplicateRows);
    }

    [Fact]
    public void Finds_mixed_values_constants_and_outliers_without_mutating_rows()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var value in new[] { "1", "1", "1", "1", "1", "100", "bad" })
            rows.Add(new Dictionary<string, string?> { ["Value"] = value, ["Constant"] = "yes" });
        var dataset = new Dataset(Guid.NewGuid(), "quality.csv", DateTimeOffset.UtcNow, ["Value", "Constant"], rows, ',');

        var findings = _profiler.GetQualityFindings(dataset);
        Assert.Contains(findings, f => f.Code == "mixed_numeric" && f.Column == "Value");
        Assert.Contains(findings, f => f.Code == "constant_column" && f.Column == "Constant");
        Assert.Equal("100", dataset.Rows[5]["Value"]);
    }

    [Fact]
    public void Generates_histogram_and_missing_value_charts()
    {
        var dataset = DatasetWithRows(
            new() { ["Age"] = "10", ["Group"] = "A" },
            new() { ["Age"] = "20", ["Group"] = null });
        Assert.Equal(10, _profiler.GenerateChart(dataset, new("histogram", "Age")).Data.Count);
        Assert.Equal(2, _profiler.GenerateChart(dataset, new("missing", "Age")).Data.Count);
    }

    [Fact]
    public void Scatter_chart_uses_numeric_x_values_instead_of_row_indexes()
    {
        var dataset = new Dataset(Guid.NewGuid(), "points.csv", DateTimeOffset.UtcNow, ["X", "Y"],
            [
                new Dictionary<string, string?> { ["X"] = "10", ["Y"] = "100" },
                new Dictionary<string, string?> { ["X"] = "20", ["Y"] = "250" }
            ], ',');

        var chart = _profiler.GenerateChart(dataset, new("scatter", "X", "Y"));

        Assert.Equal([10d, 20d], chart.Data.Select(point => point.SecondaryValue!.Value));
        Assert.Equal([100d, 250d], chart.Data.Select(point => point.Value));
    }

    [Fact]
    public void Store_keeps_datasets_isolated()
    {
        var store = new InMemoryDatasetStore();
        var first = DatasetWithRows(new Dictionary<string, string?> { ["Age"] = "1", ["Group"] = "A" });
        var second = DatasetWithRows(new Dictionary<string, string?> { ["Age"] = "2", ["Group"] = "B" });
        store.Add(first); store.Add(second);
        Assert.Equal("1", store.Get(first.Id).Rows[0]["Age"]);
        Assert.Equal("2", store.Get(second.Id).Rows[0]["Age"]);
    }

    [Fact]
    public void Store_evicts_the_oldest_dataset_after_the_retention_limit()
    {
        var store = new InMemoryDatasetStore();
        var datasets = Enumerable.Range(0, InMemoryDatasetStore.MaxDatasets + 1)
            .Select(index => new Dataset(Guid.NewGuid(), $"{index}.csv", DateTimeOffset.UtcNow.AddMinutes(index),
                ["Value"], [new Dictionary<string, string?> { ["Value"] = index.ToString() }], ','))
            .ToArray();

        foreach (var dataset in datasets) store.Add(dataset);

        Assert.Equal(InMemoryDatasetStore.MaxDatasets, store.List().Count);
        Assert.False(store.TryGet(datasets[0].Id, out _));
        Assert.True(store.TryGet(datasets[^1].Id, out _));
    }

    [Fact]
    public void Markdown_report_escapes_dataset_controlled_markup_and_uses_invariant_numbers()
    {
        var dataset = new Dataset(Guid.NewGuid(), "<script>*report*.csv", DateTimeOffset.UtcNow, ["[Score]", "Group"],
            [
                new Dictionary<string, string?> { ["[Score]"] = "1", ["Group"] = "A" },
                new Dictionary<string, string?> { ["[Score]"] = "2", ["Group"] = "B" }
            ], ',');

        var report = new MarkdownReportService(_profiler).GenerateMarkdown(dataset);

        Assert.DoesNotContain("<script>", report);
        Assert.Contains("&lt;script&gt;", report);
        Assert.Contains("\\[Score\\]", report);
        Assert.Contains("1.50", report);
    }

    [Fact]
    public void Duplicate_detection_distinguishes_null_literals_and_embedded_separators()
    {
        var dataset = DatasetWithRows(
            new() { ["Age"] = null, ["Group"] = "A" },
            new() { ["Age"] = "<NULL>", ["Group"] = "A" },
            new() { ["Age"] = "a\u001fb", ["Group"] = "c" },
            new() { ["Age"] = "a", ["Group"] = "b\u001fc" });

        Assert.Equal(0, _profiler.GetOverview(dataset).DuplicateRows);
    }

    [Fact]
    public void Numeric_detection_rejects_ambiguous_and_non_finite_values()
    {
        Assert.NotEqual(DataKind.Numeric, DataProfiler.DetectKind(["1,2", "2,3"]));
        Assert.NotEqual(DataKind.Numeric, DataProfiler.DetectKind(["NaN", "Infinity"]));
        Assert.Equal(DataKind.Numeric, DataProfiler.DetectKind(["1.2", "2.3"]));
    }

    private static Dataset DatasetWithRows(params Dictionary<string, string?>[] rows) =>
        new(Guid.NewGuid(), "test.csv", DateTimeOffset.UtcNow, ["Age", "Group"], rows, ',');
}
