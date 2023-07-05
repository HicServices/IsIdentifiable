namespace IsIdentifiable.Reporting.Destinations;

public class CsvReportDestinationOptions : ReportDestinationOptions
{
    /// <summary>
    /// If specified, the given separator will be used instead of ','.  Includes support for \t for tab and \r\n.
    /// </summary>
    public string? CsvSeparator { get; init; }
}
