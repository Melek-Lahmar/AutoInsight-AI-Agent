using System.Collections.Concurrent;
using AutoInsight.Core;

namespace AutoInsight.Infrastructure;

public sealed class InMemoryDatasetStore : IDatasetStore
{
    public const int MaxDatasets = 10;
    private readonly ConcurrentDictionary<Guid, Dataset> _datasets = new();
    private readonly object _evictionGate = new();

    public Dataset Add(Dataset dataset)
    {
        lock (_evictionGate)
        {
            _datasets[dataset.Id] = dataset;
            while (_datasets.Count > MaxDatasets)
            {
                var oldest = _datasets.Values.MinBy(item => item.UploadedAt);
                if (oldest is null || !_datasets.TryRemove(oldest.Id, out _)) break;
            }
        }
        return dataset;
    }

    public Dataset Get(Guid id) => _datasets.TryGetValue(id, out var dataset) ? dataset : throw new DatasetNotFoundException(id);
    public bool TryGet(Guid id, out Dataset? dataset) => _datasets.TryGetValue(id, out dataset);

    public IReadOnlyList<DatasetSummary> List() => _datasets.Values
        .OrderByDescending(d => d.UploadedAt)
        .Select(d => new DatasetSummary(d.Id, d.FileName, d.Rows.Count, d.Headers.Count, d.UploadedAt))
        .ToArray();
}
