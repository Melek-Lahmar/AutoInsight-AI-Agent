using System.Text.Json;
using AutoInsight.Core;
using AutoInsight.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoInsight.Tests;

public sealed class AgentToolTests
{
    [Fact]
    public async Task Demo_agent_prefers_the_profiled_numeric_column_for_a_generic_chart_request()
    {
        var store = new InMemoryDatasetStore();
        var dataset = new Dataset(Guid.NewGuid(), "scores.csv", DateTimeOffset.UtcNow, ["StudentId", "Score"],
            [new Dictionary<string, string?> { ["StudentId"] = "S1", ["Score"] = "10" }, new Dictionary<string, string?> { ["StudentId"] = "S2", ["Score"] = "20" }], ',');
        store.Add(dataset);
        var response = await new DemoAgentService(store, new DataProfiler()).ChatAsync(new(dataset.Id, "Calculate useful statistics and generate a chart."));
        Assert.Equal("Histogram of Score", response.Chart!.Title);
    }

    [Fact]
    public void Tool_returns_real_statistics_and_records_trace()
    {
        var store = new InMemoryDatasetStore();
        var dataset = new Dataset(Guid.NewGuid(), "scores.csv", DateTimeOffset.UtcNow, ["Score"],
            [new Dictionary<string, string?> { ["Score"] = "10" }, new Dictionary<string, string?> { ["Score"] = "20" }], ',');
        store.Add(dataset);
        var plugin = new DatasetAnalysisPlugin(store, new DataProfiler(), NullLogger<DatasetAnalysisPlugin>.Instance);

        var json = plugin.CalculateStatistics(dataset.Id.ToString(), "Score");
        Assert.Contains("15", json);
        Assert.Equal("calculate_statistics", Assert.Single(plugin.Executions).Tool);
        Assert.NotNull(JsonDocument.Parse(json));
    }

    [Fact]
    public void Overview_tool_never_exposes_raw_preview_rows()
    {
        var store = new InMemoryDatasetStore();
        var dataset = new Dataset(Guid.NewGuid(), "private.csv", DateTimeOffset.UtcNow, ["Secret"],
            [new Dictionary<string, string?> { ["Secret"] = "raw-private-value" }], ',');
        store.Add(dataset);
        var plugin = new DatasetAnalysisPlugin(store, new DataProfiler(), NullLogger<DatasetAnalysisPlugin>.Instance);
        var json = plugin.GetDatasetOverview(dataset.Id.ToString());
        Assert.DoesNotContain("raw-private-value", json);
        Assert.DoesNotContain("Preview", json);
    }

    [Fact]
    public void Column_information_and_chart_result_minimize_model_payloads()
    {
        var store = new InMemoryDatasetStore();
        var dataset = new Dataset(Guid.NewGuid(), "private.csv", DateTimeOffset.UtcNow, ["Student", "Score"],
            [
                new Dictionary<string, string?> { ["Student"] = "private-student-a", ["Score"] = "10" },
                new Dictionary<string, string?> { ["Student"] = "private-student-b", ["Score"] = "20" }
            ], ',');
        store.Add(dataset);
        var plugin = new DatasetAnalysisPlugin(store, new DataProfiler(), NullLogger<DatasetAnalysisPlugin>.Instance);

        var columns = plugin.GetColumnInformation(dataset.Id.ToString());
        var chartMetadata = plugin.GenerateChartSpecification(dataset.Id.ToString(), "bar", "Student");

        Assert.DoesNotContain("private-student-a", columns);
        Assert.DoesNotContain("private-student-a", chartMetadata);
        Assert.Contains("pointCount", chartMetadata);
        Assert.Contains("private-student-a", JsonSerializer.Serialize(plugin.LastChart));
    }

    [Fact]
    public void Categorical_distribution_excludes_identifier_like_values()
    {
        var store = new InMemoryDatasetStore();
        var rows = Enumerable.Range(1, 6)
            .Select(index => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["StudentId"] = $"private-id-{index}",
                ["Group"] = index <= 3 ? "A" : "B"
            }).ToArray();
        var dataset = new Dataset(Guid.NewGuid(), "private.csv", DateTimeOffset.UtcNow, ["StudentId", "Group"], rows, ',');
        store.Add(dataset);
        var plugin = new DatasetAnalysisPlugin(store, new DataProfiler(), NullLogger<DatasetAnalysisPlugin>.Instance);

        var json = plugin.GetCategoricalDistribution(dataset.Id.ToString());

        Assert.DoesNotContain("private-id-1", json);
        Assert.Contains("Group", json);
    }

    [Theory]
    [InlineData("gemini")]
    [InlineData("google")]
    [InlineData("googleai")]
    [InlineData("openai")]
    public void Ai_options_accept_supported_providers(string provider)
    {
        var options = new AiOptions { Provider = provider, Model = "test-model", ApiKey = "test" };
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void Ai_options_reject_unknown_provider()
    {
        var options = new AiOptions { Provider = "unknown", Model = "test-model", ApiKey = "test" };
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Ai_configuration_clamps_timeout_and_tool_limits()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_TIMEOUT_SECONDS"] = "1",
            ["AI_MAX_TOOL_INVOCATIONS"] = "99"
        }).Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddAutoInsightInfrastructure(configuration)
            .BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<AiOptions>>().Value;

        Assert.Equal(5, options.TimeoutSeconds);
        Assert.Equal(16, options.MaxToolInvocations);
    }

    [Fact]
    public void Tool_enforces_invocation_limit()
    {
        var store = new InMemoryDatasetStore();
        var dataset = new Dataset(Guid.NewGuid(), "scores.csv", DateTimeOffset.UtcNow, ["Score"], [], ',');
        store.Add(dataset);
        var plugin = new DatasetAnalysisPlugin(store, new DataProfiler(), NullLogger<DatasetAnalysisPlugin>.Instance, 1);
        plugin.GetDatasetOverview(dataset.Id.ToString());
        Assert.Throws<InvalidOperationException>(() => plugin.GetDatasetOverview(dataset.Id.ToString()));
    }

    [Fact]
    public void Failed_tool_call_is_not_recorded_as_a_successful_execution()
    {
        var plugin = new DatasetAnalysisPlugin(
            new InMemoryDatasetStore(),
            new DataProfiler(),
            NullLogger<DatasetAnalysisPlugin>.Instance);

        Assert.Throws<DatasetValidationException>(() => plugin.GetDatasetOverview("not-a-dataset-id"));
        Assert.Empty(plugin.Executions);
    }
}
