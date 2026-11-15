using System.Text;
using AutoInsight.Core;
using AutoInsight.Infrastructure;

namespace AutoInsight.Tests;

public sealed class CsvDatasetParserTests
{
    private readonly CsvDatasetParser _parser = new();

    [Fact]
    public async Task Parses_semicolon_csv_and_normalizes_blanks()
    {
        await using var stream = Csv("Name;Score;Group\nAda;98;A\nLinus;;B");
        var dataset = await _parser.ParseAsync(stream, "students.csv");
        Assert.Equal(';', dataset.Delimiter);
        Assert.Equal(2, dataset.Rows.Count);
        Assert.Null(dataset.Rows[1]["Score"]);
    }

    [Fact]
    public async Task Rejects_non_csv_files()
    {
        await using var stream = Csv("Name,Score\nAda,98");
        var error = await Assert.ThrowsAsync<DatasetValidationException>(() => _parser.ParseAsync(stream, "students.exe"));
        Assert.Contains(".csv", error.Message);
    }

    [Fact]
    public async Task Rejects_malformed_row_width()
    {
        await using var stream = Csv("Name,Score\nAda,98,unexpected");
        await Assert.ThrowsAsync<DatasetValidationException>(() => _parser.ParseAsync(stream, "bad.csv"));
    }

    [Fact]
    public async Task Accepts_header_only_dataset()
    {
        await using var stream = Csv("Name,Score\n");
        var dataset = await _parser.ParseAsync(stream, "empty.csv");
        Assert.Empty(dataset.Rows);
        Assert.Equal(2, dataset.Headers.Count);
    }

    private static MemoryStream Csv(string value) => new(Encoding.UTF8.GetBytes(value));
}
