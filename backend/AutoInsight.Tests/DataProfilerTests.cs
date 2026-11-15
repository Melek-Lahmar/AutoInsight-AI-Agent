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
    public void Store_keeps_datasets_isolated()
    {
        var store = new InMemoryDatasetStore();
        var first = DatasetWithRows(new Dictionary<string, string?> { ["Age"] = "1", ["Group"] = "A" });
        var second = DatasetWithRows(new Dictionary<string, string?> { ["Age"] = "2", ["Group"] = "B" });
        store.Add(first); store.Add(second);
        Assert.Equal("1", store.Get(first.Id).Rows[0]["Age"]);
        Assert.Equal("2", store.Get(second.Id).Rows[0]["Age"]);
    }

    private static Dataset DatasetWithRows(params Dictionary<string, string?>[] rows) =>
        new(Guid.NewGuid(), "test.csv", DateTimeOffset.UtcNow, ["Age", "Group"], rows, ',');
}
