using CsvHelper;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Scanners;

/// <summary>
/// Runner for reading data from CSV files and evaluating it for identifiable content
/// </summary>
public class CsvFileScanner : ResourceScannerBase, IFileScanner
{
    private readonly CultureInfo? _culture;
    private readonly int _stopAfter;

    /// <summary>
    /// Creates a new instance for reading CSV files and detecting identifiable data
    /// </summary>
    /// <param name="fileSystem"></param>
    public CsvFileScanner(
        CSVFileScannerOptions options,
        IFileSystem fileSystem,
        int stopAfter,
        params IFailureReport[] reports
    )
        : base(options, fileSystem, reports)
    {
        _culture = options.Culture;
        _stopAfter = stopAfter;
    }

    /// <inheritdoc/>
    public void Scan(IFileInfo fi)
    {
        using var stream = fi.OpenRead();
        using var streamReader = new System.IO.StreamReader(stream);

        var culture = _culture ?? CultureInfo.InvariantCulture;
        using var reader = new CsvReader(streamReader, culture);

        // TODO(rkm 2023-07-02) Check if this fails if there is only the header record
        if (!reader.Read() || !reader.ReadHeader())
            throw new Exception("Csv file had no headers");

        var headerRecord = reader.HeaderRecord!;
        Logger.Info($"Headers are:{string.Join(",", headerRecord)}");

        while (reader.Read())
        {
            foreach (var column in headerRecord)
            {
                var value = reader[column];
                if (value == null)
                    continue;

                var parts = new List<FailurePart>();
                parts.AddRange(Validate(column, value));

                if (!parts.Any())
                    continue;

                NotifyNewFailure(new Failure(parts)
                {
                    Resource = fi.FullName,
                    ResourcePrimaryKey = null,
                    ProblemField = column,
                    ProblemValue = value,
                });
            }

            NotifyDoneRows(1);

            if (_stopAfter > 0 && ItemsProcessed >= _stopAfter)
                break;
        }

        CloseReports();
    }

    protected override void DisposeImpl()
    {
        // TODO(rkm 2023-07-05)
        throw new NotImplementedException();
    }

    protected override string LogProgressNoun() => "rows";
}
