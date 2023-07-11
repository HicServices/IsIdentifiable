using CommandLine;

namespace ii.Options;

internal abstract class BaseCliVerb
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

    public BaseCliVerb(string yamlConfigPath)
    {
        _yamlConfigPath = yamlConfigPath;
    }
}
