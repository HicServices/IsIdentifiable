using CommandLine;
using IsIdentifiable.Reporting.Reports;

namespace ii.Review;

[Verb("review", HelpText = $"Review {nameof(IFailureReport)}s and produce redaction rules")]
internal class ReviewCliVerb : BaseCliVerb
{
    [Option(
        's',
        "use-system-console",
        Default = false,
        Required = false,
        HelpText = "Sets UseSystemConsole to true for Terminal.gui (i.e. uses the NetDriver which is based on System.Console)"
    )]
    public bool UseSystemConsole { get => _useSystemConsole; }

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

    private readonly bool _useSystemConsole;
    private readonly string _reportList;
    private readonly string _ignoreList;

    public ReviewCliVerb(
        bool useSystemConsole,
        string reportList,
        string ignoreList,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _useSystemConsole = useSystemConsole;
        _reportList = reportList;
        _ignoreList = ignoreList;
    }
}
