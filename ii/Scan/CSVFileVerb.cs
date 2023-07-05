using CommandLine;

namespace ii.Scan;

[Verb("csv")]
internal class CSVFileVerb : CliVerbBase
{
    [Value(
        0,
        Required = true,
        MetaName = "file-or-dir",
        HelpText = "Filename or dir to process files in"
    )]
    public string FileOrDir { get => _fileOrDir; }
    private readonly string _fileOrDir;

    [Option(
        'r',
        "recursive",
        Default = default(bool),
        HelpText = "Recursively search in directories"
    )]
    public bool Recursive { get => _recursive; }
    private readonly bool _recursive;

    [Option(
        's',
        "stop-after",
        Default = default(int),
        HelpText = "Set to stop processing after x records e.g., only evalute top 1000 records of a table/file"
    )]
    public int StopAfter { get => _stopAfter; }
    private readonly int _stopAfter;

    public CSVFileVerb(
        string fileOrDir,
        bool recursive,
        int stopAfter,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _fileOrDir = fileOrDir;
        _recursive = recursive;
        _stopAfter = stopAfter;
    }
}
