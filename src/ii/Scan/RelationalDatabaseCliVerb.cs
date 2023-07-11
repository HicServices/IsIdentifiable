using CommandLine;
using ii.Options;

namespace ii.Scan;

[Verb("relational-database")]
internal class RelationalDatabaseCliVerb : BaseCliVerb, IDatabaseTargetOptions
{
    [Value(
        0,
        Required = true,
        HelpText = "The unqualified name of the table to evaluate"
    )]
    public string TableName { get => _tableName; }

    [Value(
        1,
        Required = false,
        HelpText = "DBMS type - 'MicrosoftSQLServer','MySql', 'PostgreSql' or 'Oracle'"
    )]
    public string? DatabaseType { get => _databaseType; }

    [Value(
        2,
        Required = false,
        HelpText = "Full connection string to the database storing the table to be evaluated"
    )]
    public string? DatabaseConnectionString { get => _databaseConnectionString; }

    [Option(
        't',
        "target-database",
        HelpText = "Target the database named in the configuration file instead of the other CLI arguments"
    )]
    public string? TargetDatabaseName { get => _targetDatabaseName; }

    [Option(
        's',
        "stop-after",
        Default = default(int),
        HelpText = "Set to stop processing after x records e.g., only evaluate top 1000 records of a table/file"
    )]
    public int StopAfter { get => _stopAfter; }

    private readonly string _tableName;
    private readonly string? _databaseType;
    private readonly string? _databaseConnectionString;
    private readonly string? _targetDatabaseName;
    private readonly int _stopAfter;

    public RelationalDatabaseCliVerb(
        string tableName,
        string? databaseType,
        string? databaseConnectionString,
        string? targetDatabaseName,
        int stopAfter,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _tableName = tableName;
        _databaseType = databaseType;
        _databaseConnectionString = databaseConnectionString;
        _targetDatabaseName = targetDatabaseName;
        _stopAfter = stopAfter;
    }
}
