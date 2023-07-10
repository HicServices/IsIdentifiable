using CommandLine;
using ii.Options;

namespace ii.Redact;

[Verb("redact", HelpText = $"Redact values in a database based on a FailureReport CSV")]
internal class RedactCliVerb : BaseCliVerb, IDatabaseTargetOptions
{
    [Value(
        0,
        Required = true,
        HelpText = "CSV file containing failures to redact from the database"
    )]
    public string FailuresCsv { get => _failuresCsv; }

    [Value(
        1,
        Required = true,
        HelpText = "The unqualified name of the table to evaluate"
    )]
    public string TableName { get => _tableName; }

    [Value(
        2,
        Required = false,
        HelpText = "DBMS type - 'MicrosoftSQLServer','MySql', 'PostgreSql' or 'Oracle'"
    )]
    public string? DatabaseType { get => _databaseType; }

    [Value(
        3,
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
        'r',
        "report-list",
        Required = false,
        Default = "ReportList.yaml",
        HelpText = "File containing rules for when to issue UPDATE statements"
    )]
    public string ReportList { get => _reportList; }

    [Option(
        'i',
        "ignore-list",
        Required = false,
        Default = "IgnoreList.yaml",
        HelpText = "File containing rules for ignoring PII during redaction"
    )]
    public string IgnoreList { get => _ignoreList; }

    [Option(
        'o',
        "output-path",
        Required = false,
        HelpText = "[Optional] Runs the application automatically processing existing update/ignore rules.  Failures not matching either are written to a new file with this path"
    )]
    public string? OutputPath { get => _outputPath; }

    private readonly string _failuresCsv;
    private readonly string _tableName;
    private readonly string? _databaseType;
    private readonly string? _databaseConnectionString;
    private readonly string? _targetDatabaseName;
    private readonly string _reportList;
    private readonly string _ignoreList;
    private readonly string? _outputPath;

    public RedactCliVerb(
        string failuresCsv,
        string tableName,
        string? databaseType,
        string? databaseConnectionString,
        string? targetDatabaseName,
        string reportList,
        string ignoreList,
        string? outputPath,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _failuresCsv = failuresCsv;
        _tableName = tableName;
        _databaseType = databaseType;
        _databaseConnectionString = databaseConnectionString;
        _targetDatabaseName = targetDatabaseName;
        _ignoreList = ignoreList;
        _reportList = reportList;
        _outputPath = outputPath;
    }
}
