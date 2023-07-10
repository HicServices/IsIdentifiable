using IsIdentifiable.Reporting.Reports;
using System;
using System.Data;

namespace IsIdentifiable.Reporting.Destinations;

/// <summary>
/// Describes a place to store the results of a <see cref="FailureReport"/> (e.g. CSV / Database)
/// </summary>
public interface IReportDestination : IDisposable
{
    /// <summary>
    /// Write the report header.
    /// </summary>
    /// <param name="headers"></param>
    void WriteHeader(params string[] headers);

    /// <summary>
    /// Write either the entire report (possibly including a header), or a number of report items.
    /// </summary>
    /// <param name="items"></param>
    void WriteItems(DataTable items);
}
