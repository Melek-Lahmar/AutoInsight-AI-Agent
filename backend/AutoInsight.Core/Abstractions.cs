namespace AutoInsight.Core;

public interface IDatasetStore
{
    Dataset Add(Dataset dataset);
    Dataset Get(Guid id);
    bool TryGet(Guid id, out Dataset? dataset);
    IReadOnlyList<DatasetSummary> List();
}

public interface ICsvDatasetParser
{
    Task<Dataset> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}

public interface IDataProfiler
{
    DatasetOverview GetOverview(Dataset dataset, int previewRows = 12);
    IReadOnlyList<ColumnProfile> GetColumnProfiles(Dataset dataset);
    IReadOnlyList<QualityFinding> GetQualityFindings(Dataset dataset);
    ChartSpecification GenerateChart(Dataset dataset, VisualizationRequest request);
}

public interface IAgentService
{
    Task<AgentResponse> ChatAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    string GenerateMarkdown(Dataset dataset);
}
