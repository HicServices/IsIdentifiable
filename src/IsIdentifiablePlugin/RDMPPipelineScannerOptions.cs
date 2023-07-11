using IsIdentifiable.Options;
using System;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiablePlugin;

internal class YamlOptions
{
    [YamlMember(Alias = YamlOptionsConstants.IS_IDENTIFIABLE_YAML_KEY)]
    public IsIdentifiableRDMPOptions? IsIdentifiableOptions { get; init; }

    public static IsIdentifiableRDMPOptions LoadFrom(IFileInfo fileInfo)
    {
        var yamlOptions = YamlOptionsExtensions.Load<YamlOptions>(fileInfo);
        return yamlOptions?.IsIdentifiableOptions ?? throw new ArgumentException($"Could not find key '{YamlOptionsConstants.IS_IDENTIFIABLE_YAML_KEY}' in file");
    }
}

internal class IsIdentifiableRDMPOptions : IsIdentifiableOptions
{
    [YamlMember(Alias = "RDMPPipelineScanner")]
    public RDMPPipelineScannerOptions? RDMPPipelineScannerOptions { get; init; }
}

internal class RDMPPipelineScannerOptions : ResourceScannerBaseOptions
{

}
