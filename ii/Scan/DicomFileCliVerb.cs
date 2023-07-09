using CommandLine;

namespace ii.Scan;

[Verb("dcm", HelpText = "Scan DICOM files")]
internal class DicomFileCliVerb : BaseCliVerb
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

    private readonly string _fileOrDir;
    private readonly bool _recursive;

    public DicomFileCliVerb(
        string fileOrDir,
        bool recursive,
        string yamlConfigPath
    )
        : base(yamlConfigPath)
    {
        _fileOrDir = fileOrDir;
        _recursive = recursive;
    }
}
