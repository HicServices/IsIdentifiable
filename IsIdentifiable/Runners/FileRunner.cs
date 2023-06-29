using CsvHelper;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Runners;

/// <summary>
/// Runner for reading data from CSV files and evaluating it for identifiable content
/// </summary>
public class FileRunner : RunnerBase
{
    private readonly IsIdentifiableFileOptions _opts;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Creates a new instance for reading the CSV <see cref="IsIdentifiableFileOptions.File"/>
    /// and detecting identifiable data
    /// </summary>
    /// <param name="opts"></param>
    /// <param name="fileSystem"></param>
    public FileRunner(IsIdentifiableFileOptions opts, IFileSystem fileSystem)
        : base(opts, fileSystem)
    {
        _opts = opts;
        LogProgressNoun = "rows";
    }

    /// <summary>
    /// Opens <see cref="IsIdentifiableFileOptions.File"/> and evaluates all data within
    /// for identifiable information.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public override int Run()
    {
        using var stream = _opts.File.OpenRead();
        using var fs = new System.IO.StreamReader(stream);
        var culture = string.IsNullOrWhiteSpace(_opts.Culture) ? CultureInfo.CurrentCulture : CultureInfo.GetCultureInfo(_opts.Culture);

        using var r = new CsvReader(fs, culture);

        if (!r.Read() || !r.ReadHeader())
            throw new Exception("Csv file had no headers");

        _logger.Info($"Headers are:{string.Join(",", r.HeaderRecord)}");

        var done = 0;

        while (r.Read())
        {
            foreach (var failure in GetFailuresIfAny(r))
                AddToReports(failure);

            done++;

            // if we have done all we were asked to do then stop
            if (_opts.Top > 0 && done >= _opts.Top)
                break;
        }

        CloseReports();

        return 0;
    }

    private IEnumerable<Failure> GetFailuresIfAny(CsvReader r)
    {
        foreach (var h in r.HeaderRecord)
        {
            var parts = new List<FailurePart>();

            parts.AddRange(Validate(h, r[h]));

            if (parts.Any())
                yield return new Failure(parts)
                {
                    Resource = _opts.File.FullName,
                    ResourcePrimaryKey = "Unknown",
                    ProblemValue = r[h],
                    ProblemField = h
                };
        }

        DoneRows(1);
    }
}
