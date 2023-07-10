namespace ii.Options;

public interface IDatabaseTargetOptions
{
    string? DatabaseType { get; }

    string? DatabaseConnectionString { get; }

    string? TargetDatabaseName { get; }
}
