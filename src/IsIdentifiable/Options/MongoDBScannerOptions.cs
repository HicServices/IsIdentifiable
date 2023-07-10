namespace IsIdentifiable.Options;

public sealed class MongoDBScannerOptions : ResourceScannerBaseOptions
{
    /// <summary>
    /// The MongoDB database to connect to
    /// </summary>
    public string? MongoDBConnectionString { get; init; }

    /// <summary>
    /// The database to connect to
    /// </summary>
    public string? DatabaseName { get; init; }

    /// <summary>
    /// The name of the collection which should be queried
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The batch size to set on the MongoDb fetch query
    /// </summary>
    public int MongoDbBatchSize { get; init; } = 0;

    /// <summary>
    /// If set will use the max. number of threads available, otherwise defaults to half the available threads
    /// </summary>
    public bool UseMaxThreads { get; init; } = false;

    /// <summary>
    /// If set then the MongoDb documents are expected to be serialized DICOM images and will be processed appropriately.
    /// </summary>
    public bool DocumentsAreDicom { get; init; } = true;
}
