using AutoInsight.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoInsight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAutoInsightInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(options =>
        {
            options.Provider = configuration["AI_PROVIDER"] ?? configuration["AI:Provider"] ?? "";
            options.Model = configuration["AI_MODEL"] ?? configuration["AI:Model"] ?? "";
            options.ApiKey = configuration["AI_API_KEY"] ?? configuration["AI:ApiKey"] ?? "";
            if (int.TryParse(configuration["AI_TIMEOUT_SECONDS"], out var timeout)) options.TimeoutSeconds = timeout;
            if (int.TryParse(configuration["AI_MAX_TOOL_INVOCATIONS"], out var max)) options.MaxToolInvocations = max;
        });
        services.AddSingleton<IDatasetStore, InMemoryDatasetStore>();
        services.AddSingleton<ICsvDatasetParser, CsvDatasetParser>();
        services.AddSingleton<IDataProfiler, DataProfiler>();
        services.AddSingleton<IReportService, MarkdownReportService>();
        services.AddSingleton<DemoAgentService>();
        services.AddSingleton<SemanticKernelAgentService>();
        services.AddSingleton<IAgentService, AgentRouterService>();
        return services;
    }
}
