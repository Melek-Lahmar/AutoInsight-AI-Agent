using System.Text;
using AutoInsight.Core;
using CsvHelper;

namespace AutoInsight.Infrastructure;

public sealed class CsvDatasetParser : ICsvDatasetParser
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRows = 50_000;
    public const int MaxColumns = 100;
    private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];

    public async Task<Dataset> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new DatasetValidationException("Only .csv files are accepted.");

        await using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + bytesRead > MaxFileBytes)
                throw new DatasetValidationException("The CSV file exceeds the 5 MB upload limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
        if (buffer.Length == 0) throw new DatasetValidationException("The uploaded CSV file is empty.");

        buffer.Position = 0;
        string? firstLine;
        using (var probe = new StreamReader(buffer, Encoding.UTF8, true, leaveOpen: true))
            firstLine = await probe.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(firstLine)) throw new DatasetValidationException("The CSV header is missing.");
        var delimiter = DetectDelimiter(firstLine);

        buffer.Position = 0;
        var options = CsvReaderOptions.Default with
        {
            Separator = delimiter,
            TrimWhitespace = true,
            SkipEmptyLines = true,
            MalformedData = CsvMalformedDataHandling.Throw,
            FieldCountMismatch = CsvFieldCountHandling.Throw,
            MissingField = CsvMissingFieldHandling.Throw
        };

        try
        {
            using var csv = CsvReader.Create(buffer, options, leaveOpen: true);
            await csv.ReadHeaderAsync(cancellationToken);
            var headers = csv.Headers.Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"Column{i + 1}" : h.Trim()).ToArray();
            if (headers.Length == 0) throw new DatasetValidationException("No columns were detected.");
            if (headers.Length > MaxColumns) throw new DatasetValidationException($"The CSV contains more than {MaxColumns} columns.");
            if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                throw new DatasetValidationException("Column names must be unique.");

            var rows = new List<IReadOnlyDictionary<string, string?>>();
            while (await csv.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count >= MaxRows) throw new DatasetValidationException($"The CSV contains more than {MaxRows:N0} rows.");
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length; i++) row[headers[i]] = Normalize(csv.GetStringOrNull(i));
                rows.Add(row);
            }

            return new Dataset(Guid.NewGuid(), Path.GetFileName(fileName), DateTimeOffset.UtcNow, headers, rows, delimiter);
        }
        catch (DatasetValidationException) { throw; }
        catch (Exception ex) when (ex is CsvFormatException or CsvValidationException or DecoderFallbackException)
        {
            throw new DatasetValidationException("The CSV could not be parsed. Verify its delimiter, quoting, encoding, and row widths.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static char DetectDelimiter(string line)
    {
        var scores = CandidateDelimiters.ToDictionary(c => c, c => CountOutsideQuotes(line, c));
        var best = scores.MaxBy(pair => pair.Value);
        return best.Value > 0 ? best.Key : ',';
    }

    private static int CountOutsideQuotes(string value, char candidate)
    {
        var quoted = false;
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                if (quoted && i + 1 < value.Length && value[i + 1] == '"') i++;
                else quoted = !quoted;
            }
            else if (!quoted && value[i] == candidate) count++;
        }
        return count;
    }
}
