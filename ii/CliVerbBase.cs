using CommandLine;

namespace ii;

internal abstract class CliVerbBase
{
    [Option(
        'y',
        "yaml-config-path",
        Default = "default.yaml",
        Required = false,
        HelpText = "Path to a yaml config file"
    )]
    public string YamlConfigPath { get => _yamlConfigPath; }
    private readonly string _yamlConfigPath;

    public CliVerbBase(string yamlConfigPath)
    {
        _yamlConfigPath = yamlConfigPath;
    }
}
