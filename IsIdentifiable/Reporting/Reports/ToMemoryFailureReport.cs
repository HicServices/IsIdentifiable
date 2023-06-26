using System.Collections.Generic;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Destinations;

namespace IsIdentifiable.Reporting.Reports;

/// <summary>
/// <see cref="IFailureReport"/> which simply captures all <see cref="Failure"/>
/// and stores in a list (See <see cref="Failures"/>).  Useful for testing or
/// if you want to send the <see cref="Failures"/> on for further processing that
/// does not involve a standard <see cref="IReportDestination"/>
/// </summary>
public class ToMemoryFailureReport : IFailureReport
{
    /// <summary>
    /// All identifiable information detected by the runner
    /// </summary>
    public List<Failure> Failures { get; } = new();


    /// <summary>
    /// Overridden to do nothing. This report does not support routing to 
    /// destinations
    /// </summary>
    public void AddDestinations(IsIdentifiableBaseOptions options)
    {
            
    }

    /// <summary>
    /// Overridden to do nothing.
    /// </summary>
    public void DoneRows(int numberDone)
    {
            
    }

    /// <summary>
    /// Adds the <paramref name="failure"/> to <see cref="Failures"/>
    /// </summary>
    /// <param name="failure"></param>
    public void Add(Failure failure)
    {
        Failures.Add(failure);
    }

    /// <summary>
    /// Overridden to do nothing.
    /// </summary>
    public void CloseReport()
    {
            
    }
}