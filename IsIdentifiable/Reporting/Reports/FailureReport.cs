using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Destinations;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace IsIdentifiable.Reporting.Reports;

/// <summary>
/// Abstract base for classes that aggregate or persist multiple
/// <see cref="Failure"/> instances detected when performing an IsIdentifiable
/// analysis on some data.
/// </summary>
public abstract class FailureReport : IFailureReport
{
    /// <summary>
    /// Short human readable name that describes the kind of aggregation or persistence of
    /// <see cref="Failure"/> objects that this report conducts
    /// </summary>
    public readonly string ReportName;

    /// <summary>
    /// FileSystem to use for I/O
    /// </summary>
    protected readonly IFileSystem FileSystem;

    /// <summary>
    /// The output adapters to which this reports data will be written e.g. CSV, database etc
    /// </summary>
    public List<IReportDestination> Destinations = new();


    /// <summary>
    /// Creates a new report aimed at the given resource (e.g. "MR_ImageTable")
    /// </summary>
    /// <param name="targetName"></param>
    /// <param name="fileSystem"></param>
    protected FailureReport(string targetName, IFileSystem fileSystem)
    {
        ReportName = targetName + GetType().Name;
        FileSystem = fileSystem;
    }

    /// <summary>
    /// Creates report destinations. Can be overridden to add headers or to initialize the destination in some way.
    /// </summary>
    /// <param name="opts"></param>
    public virtual void AddDestinations(IsIdentifiableOptions opts)
    {
        // TODO(rkm 2023-07-05) Refactor this

        //IReportDestination destination;

        //// Default is to write out CSV results
        //if (!string.IsNullOrWhiteSpace(opts.DestinationCsvFolder))
        //    destination = new CsvDestination(opts, ReportName, FileSystem, true);
        //else if (!string.IsNullOrWhiteSpace(opts.DestinationConnectionString))
        //    destination = new DatabaseDestination(opts, ReportName, FileSystem);
        //else
        //{
        //    opts.DestinationCsvFolder = Environment.CurrentDirectory;
        //    destination = new CsvDestination(opts, ReportName, FileSystem);
        //}

        //Destinations.Add(destination);
    }

    /// <summary>
    /// Override to log or flush output streams.  Called periodically.
    /// </summary>
    /// <param name="numberDone">Number of rows done since the last call to this method</param>
    public virtual void DoneRows(int numberDone) { }

    /// <inheritdoc/>
    public abstract void Add(Failure failure);


    /// <inheritdoc/>
    public void CloseReport()
    {
        CloseReportBase();
        Destinations.ForEach(d => d.Dispose());
    }

    /// <summary>
    /// Writes out (the rest of) the report before exiting.  Override to
    /// flush output streams, close files, commit transactions etc.
    /// </summary>
    protected abstract void CloseReportBase();
}
