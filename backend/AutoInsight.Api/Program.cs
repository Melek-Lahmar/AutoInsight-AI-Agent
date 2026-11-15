using System.Text;
using AutoInsight.Core;
using AutoInsight.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = CsvDatasetParser.MaxFileBytes + 64 * 1024);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = CsvDatasetParser.MaxFileBytes + 64 * 1024);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AutoInsight API", Version = "v1", Description = "Local, privacy-aware CSV profiling and agentic analysis API." });
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAutoInsightInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors("frontend");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/health", (IOptions<AiOptions> options) => Results.Ok(new
{
    status = "healthy",
    service = "AutoInsight.Api",
    utcTime = DateTimeOffset.UtcNow,
    aiModeConfigured = options.Value.IsConfigured
})).WithName("Health");

app.MapGet("/api/config", (IOptions<AiOptions> options) => Results.Ok(new
{
    demoModeAvailable = true,
    aiModeConfigured = options.Value.IsConfigured,
    provider = options.Value.IsConfigured ? options.Value.Provider : null,
    model = options.Value.IsConfigured ? options.Value.Model : null,
    privacy = "Raw dataset rows remain local. AI mode receives the user question and tool-returned summaries only."
}));

app.MapGet("/api/datasets", (IDatasetStore store) => Results.Ok(store.List()));

app.MapPost("/api/datasets/upload", async (IFormFile file, ICsvDatasetParser parser, IDatasetStore store, IDataProfiler profiler, CancellationToken cancellationToken) =>
{
    if (file.Length == 0) throw new DatasetValidationException("Choose a non-empty CSV file.");
    if (file.Length > CsvDatasetParser.MaxFileBytes) throw new DatasetValidationException("The CSV file exceeds the 5 MB upload limit.");
    await using var stream = file.OpenReadStream();
    var dataset = await parser.ParseAsync(stream, file.FileName, cancellationToken);
    store.Add(dataset);
    return Results.Created($"/api/datasets/{dataset.Id}/overview", new UploadResult(profiler.GetOverview(dataset), profiler.GetColumnProfiles(dataset)));
}).DisableAntiforgery().WithName("UploadDataset");

app.MapGet("/api/datasets/{id:guid}/overview", (Guid id, IDatasetStore store, IDataProfiler profiler) => Results.Ok(profiler.GetOverview(store.Get(id))));
app.MapGet("/api/datasets/{id:guid}/columns", (Guid id, IDatasetStore store, IDataProfiler profiler) => Results.Ok(profiler.GetColumnProfiles(store.Get(id))));
app.MapGet("/api/datasets/{id:guid}/statistics", (Guid id, IDatasetStore store, IDataProfiler profiler) =>
    Results.Ok(profiler.GetColumnProfiles(store.Get(id)).Where(c => c.Statistics is not null).Select(c => new { c.Name, c.Statistics })));
app.MapGet("/api/datasets/{id:guid}/quality", (Guid id, IDatasetStore store, IDataProfiler profiler) => Results.Ok(profiler.GetQualityFindings(store.Get(id))));
app.MapPost("/api/datasets/{id:guid}/visualizations", (Guid id, VisualizationRequest request, IDatasetStore store, IDataProfiler profiler) =>
    Results.Ok(profiler.GenerateChart(store.Get(id), request)));

app.MapPost("/api/agent/chat", async (AgentRequest request, IAgentService agent, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message)) throw new DatasetValidationException("Enter an analysis question.");
    if (request.Message.Length > 2_000) throw new DatasetValidationException("The analysis question must be 2,000 characters or fewer.");
    return Results.Ok(await agent.ChatAsync(request, cancellationToken));
});

app.MapPost("/api/reports/generate", (ReportRequest request, IDatasetStore store, IReportService reports) =>
{
    var dataset = store.Get(request.DatasetId);
    var bytes = Encoding.UTF8.GetBytes(reports.GenerateMarkdown(dataset));
    var safeName = Path.GetFileNameWithoutExtension(dataset.FileName).Replace(' ', '-');
    return Results.File(bytes, "text/markdown; charset=utf-8", $"autoinsight-{safeName}-report.md");
});

app.Run();

public partial class Program;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var (status, title) = exception switch
            {
                DatasetNotFoundException => (StatusCodes.Status404NotFound, "Dataset not found"),
                DatasetValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
                BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
            };
            if (status == 500) logger.LogError(exception, "Unhandled API exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else logger.LogWarning("Rejected API request {Method} {Path}: {Reason}", context.Request.Method, context.Request.Path, exception.Message);
            if (context.Response.HasStarted) throw;
            context.Response.StatusCode = status;
            await Results.Problem(statusCode: status, title: title,
                detail: status == 500 ? "The request could not be completed." : exception.Message,
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }
}
