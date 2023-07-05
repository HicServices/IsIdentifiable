namespace IsIdentifiable.Options;

public interface IFileScannerOptions
{
    /// <summary>
    /// Search pattern for files
    /// </summary>
    string SearchPattern { get; init; }

    /// <summary>
    /// Whether to immediately stop if any exceptions are raised while processing
    /// </summary>
    bool StopOnError { get; init; }
}
