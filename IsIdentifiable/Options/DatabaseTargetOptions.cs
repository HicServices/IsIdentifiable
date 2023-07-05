using FAnsi;

namespace IsIdentifiable.Options;

public sealed class DatabaseTargetOptions
{
    /// <summary>
    /// The human-friendly name for this database
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The type of Relational database
    /// </summary>
    public DatabaseType? DatabaseType { get; init; }

    /// <summary>
    /// Full connection string to the database storing the table to be evaluated
    /// </summary>
    public string? DatabaseConnectionString { get; init; }
}
