using CommandLine;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Reporting.Destinations;

/// <summary>
/// <see cref="ReportDestination"/> that outputs IsIdentifiable reports to a text file
/// </summary>
public class CsvDestination : ReportDestination
{
    private readonly IFileInfo _destinationCsvFile;
    private readonly string? _csvSeparator;
    private System.IO.StreamWriter? _streamwriter;
    private readonly CsvConfiguration? _csvConfiguration;
    private CsvWriter? _csvWriter;
    private readonly object _oHeaderLock = new();
    private bool _headerWritten;

    /// <summary>
    /// Creates a new report destination in which values/aggregates are written to CSV (at <see cref="destinationFile"/>)
    /// </summary>
    /// <param name="options"></param>
    /// <param name="csvConfiguration"></param>
    public CsvDestination(
        CsvReportDestinationOptions options,
        IFileInfo destinationCsvFile,
        CsvConfiguration? csvConfiguration = null
    )
        : base(options)
    {
        _destinationCsvFile = destinationCsvFile;
        _csvSeparator = options.CsvSeparator;
        _csvConfiguration = csvConfiguration;
    }

    /// <summary>
    /// Writes the headings required by the report into the CSV.
    /// If the file does not exist yet it will be automatically created
    /// </summary>
    /// <param name="headers"></param>
    public override void WriteHeader(params string[] headers)
    {
        lock (_oHeaderLock)
        {
            if (_headerWritten)
                return;

            _headerWritten = true;

            CsvConfiguration csvconf;
            if (_csvConfiguration != null)
            {
                csvconf = _csvConfiguration;
            }
            // If there is an overriding separator and it's not a comma, then use the users desired delimiter string
            else if (!string.IsNullOrWhiteSpace(_csvSeparator) && !_csvSeparator.Trim().Equals(","))
            {
                csvconf = new CsvConfiguration(System.Globalization.CultureInfo.CurrentCulture)
                {
                    Delimiter = _csvSeparator.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n"),
                    ShouldQuote = _ => false,
                };
            }
            else
            {
                csvconf = new CsvConfiguration(System.Globalization.CultureInfo.CurrentCulture);
            }

            var stream = _destinationCsvFile.OpenWrite();
            _streamwriter = new System.IO.StreamWriter(stream);
            _csvWriter = new CsvWriter(_streamwriter, csvconf);
            WriteRow(headers);
        }
    }

    /// <summary>
    /// Appends the report aggregates/items data to the CSV
    /// </summary>
    /// <param name="items"></param>
    public override void WriteItems(DataTable items)
    {
        if (!_headerWritten)
            WriteHeader((from dc in items.Columns.Cast<DataColumn>() select dc.ColumnName).ToArray());

        foreach (DataRow row in items.Rows)
            WriteRow(row.ItemArray);
    }

    /// <summary>
    /// Flushes and disposes of IO handles to <see cref="ReportPath"/>
    /// </summary>
    public override void Dispose()
    {
        GC.SuppressFinalize(this);

        _csvWriter?.Dispose();
        _streamwriter?.Dispose();
    }

    private void WriteRow(IEnumerable<object> rowItems)
    {
        foreach (var item in rowItems)
            _csvWriter!.WriteField(StripWhitespace(item));

        _csvWriter!.NextRecord();
    }
}
