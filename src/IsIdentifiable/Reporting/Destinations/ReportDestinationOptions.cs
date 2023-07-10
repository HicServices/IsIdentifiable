namespace IsIdentifiable.Reporting.Destinations;

public class ReportDestinationOptions
{
    /// <summary>
    /// If specified all tabs, newlines (\r and \n) and 2+ spaces will be stripped from the values written as output (applies to all output formats)
    /// </summary>
    public bool StripWhitespaceOnWrite { get; init; } = false;
}
