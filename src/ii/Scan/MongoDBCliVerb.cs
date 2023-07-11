using CommandLine;
using ii.Options;
using IsIdentifiable.Reporting.Reports;

namespace ii.Scan;

[Verb("mongodb")]
internal class MongoDBCliVerb : BaseCliVerb
{
    [Value(
        0,
        Required = false,
        MetaName = "query-file",
        HelpText = "Path to a file containing the query to execute"
    )]
    public string? QueryFile { get => _queryFile; }

    [Option(
        't',
        "tree-report",
        Default = false,
        Required = false,
        HelpText = $"Generate a {nameof(TreeFailureReport)}"
    )]
    public bool GenerateTreeReport { get => _generateTreeReport; }

    private readonly string? _queryFile;
    private readonly bool _generateTreeReport;

    public MongoDBCliVerb(
        string? queryFile,
        bool generateTreeReport,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _queryFile = queryFile;
        _generateTreeReport = generateTreeReport;
    }
}
