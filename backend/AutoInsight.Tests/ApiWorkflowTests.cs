using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoInsight.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AutoInsight.Tests;

public sealed class ApiWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public ApiWorkflowTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Complete_demo_workflow_succeeds()
    {
        var csv = "StudentId,StudyHours,FinalGrade,Target\nS001,5,82,Pass\nS002,,71,Pass\nS002,,71,Pass\nS003,30,95,Pass";
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "students.csv");
        var uploadResponse = await _client.PostAsync("/api/datasets/upload", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var upload = JsonSerializer.Deserialize<UploadResult>(await uploadResponse.Content.ReadAsStringAsync(), Json)!;
        Assert.Equal(4, upload.Overview.RowCount);

        var overview = await _client.GetFromJsonAsync<DatasetOverview>($"/api/datasets/{upload.Overview.Id}/overview", Json);
        Assert.Equal(1, overview!.DuplicateRows);
        var quality = await _client.GetFromJsonAsync<QualityFinding[]>($"/api/datasets/{upload.Overview.Id}/quality", Json);
        Assert.Contains(quality!, f => f.Code == "missing_values");

        var chartResponse = await _client.PostAsJsonAsync($"/api/datasets/{upload.Overview.Id}/visualizations", new VisualizationRequest("histogram", "FinalGrade"));
        chartResponse.EnsureSuccessStatusCode();
        var chart = await chartResponse.Content.ReadFromJsonAsync<ChartSpecification>(Json);
        Assert.NotEmpty(chart!.Data);

        var agentResponse = await _client.PostAsJsonAsync("/api/agent/chat", new AgentRequest(upload.Overview.Id, "Find quality problems and chart the FinalGrade distribution", "demo"));
        agentResponse.EnsureSuccessStatusCode();
        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentResponse>(Json);
        Assert.Equal("demo", agent!.Mode);
        Assert.False(agent.UsedExternalProvider);
        Assert.NotEmpty(agent.ToolExecutions);

        var report = await _client.PostAsJsonAsync("/api/reports/generate", new ReportRequest(upload.Overview.Id));
        report.EnsureSuccessStatusCode();
        Assert.Contains("Dataset overview", await report.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_dataset_returns_safe_problem_details()
    {
        var response = await _client.GetAsync($"/api/datasets/{Guid.NewGuid()}/overview");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dataset not found", body);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ai_mode_without_credentials_returns_a_safe_configuration_error()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Value\n1")), "file", "tiny.csv");
        var upload = await (await _client.PostAsync("/api/datasets/upload", form)).Content.ReadFromJsonAsync<UploadResult>(Json);
        var response = await _client.PostAsJsonAsync("/api/agent/chat", new AgentRequest(upload!.Overview.Id, "Analyze this dataset", "ai"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not configured", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API_KEY", body, StringComparison.OrdinalIgnoreCase);
    }
}
