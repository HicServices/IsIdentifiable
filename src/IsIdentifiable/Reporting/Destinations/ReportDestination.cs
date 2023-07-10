using System.Data;
using System.Text.RegularExpressions;

namespace IsIdentifiable.Reporting.Destinations;

/// <summary>
/// Abstract implementation of <see cref="IReportDestination"/>.  When implemented
/// in a derrived class allows persistence of IsIdentifiable reports (e.g. to a
/// CSV file or database table).
/// </summary>
public abstract class ReportDestination : IReportDestination
{
    /// <summary>
    /// The options used to run IsIdentifiable
    /// </summary>
    protected ReportDestinationOptions Options { get; }

    private readonly Regex _multiSpaceRegex = new(" {2,}");

    /// <summary>
    /// Initializes the report destination and sets <see cref="Options"/>
    /// </summary>
    /// <param name="options"></param>
    protected ReportDestination(ReportDestinationOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Override to output the column <paramref name="headers"/> e.g. as the first line of a CSV
    /// </summary>
    /// <param name="headers"></param>
    public virtual void WriteHeader(params string[] headers) { }

    /// <summary>
    /// Override to output the given <paramref name="batch"/> of rows.  Column names will match
    /// <see cref="WriteHeader(string[])"/>.  Each row contains report data that must be persisted
    /// </summary>
    /// <param name="batch"></param>
    public abstract void WriteItems(DataTable batch);

    /// <summary>
    /// Override to perform any tidyup on the destination e.g. close file handles / end transactions
    /// </summary>
    public virtual void Dispose() { }

    /// <summary>
    /// Returns <paramref name="o"/> with whitespace stripped (if it is a string and <see cref="ReportDestinationOptions.StripWhitespaceOnWrite"/>
    /// is set on command line options).
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    protected object StripWhitespace(object o)
    {
        if (o is string s && Options.StripWhitespaceOnWrite)
            return _multiSpaceRegex.Replace(s.Replace("\t", "").Replace("\r", "").Replace("\n", ""), " ");

        return o;
    }
}
