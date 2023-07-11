using CommandLine;
using ii.Options;

namespace ii.Scan;

[Verb("csv")]
internal class CSVFileCliVerb : BaseCliVerb
{
    [Value(
        0,
        Required = true,
        MetaName = "file-or-dir",
        HelpText = "Filename or dir to process files in"
    )]
    public string FileOrDir { get => _fileOrDir; }

    [Option(
        'r',
        "recursive",
        Default = default(bool),
        HelpText = "Recursively search in directories"
    )]
    public bool Recursive { get => _recursive; }

    [Option(
        's',
        "stop-after",
        Default = default(int),
        HelpText = "Set to stop processing after x records e.g., only evaluate top 1000 records of a table/file"
    )]
    public int StopAfter { get => _stopAfter; }

    private readonly string _fileOrDir;
    private readonly bool _recursive;
    private readonly int _stopAfter;

    public CSVFileCliVerb(
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
