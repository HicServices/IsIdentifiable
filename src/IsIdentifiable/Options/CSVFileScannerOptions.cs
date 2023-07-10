using System.Globalization;

namespace IsIdentifiable.Options;

public sealed class CSVFileScannerOptions : ResourceScannerBaseOptions, IFileScannerOptions
{
    /// <summary>
    /// The culture of dates, numbers etc if different from system culture
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public bool StopOnError { get; init; } = false;

    /// <inheritdoc/>
    public string SearchPattern { get; init; } = DEFAULT_CSV_PREFIX;


    private const string DEFAULT_CSV_PREFIX = "*.csv";
}
