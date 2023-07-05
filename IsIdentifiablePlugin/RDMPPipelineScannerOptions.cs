using IsIdentifiable.Options;
using YamlDotNet.Serialization;

namespace IsIdentifiablePlugin;

internal sealed class YamlRDMPOptions
{
    [YamlMember(Alias = YamlOptions.IS_IDENTIFIABLE_YAML_KEY)]
    public IsIdentifiableRDMPOptions? IsIdentifiableRDMPOptions { get; init; }
}

internal class IsIdentifiableRDMPOptions : IsIdentifiableOptions
{
    [YamlMember(Alias = "RDMPPipelineScanner")]
    public RDMPPipelineScannerOptions? RDMPPipelineScannerOptions { get; init; }
}

internal sealed class RDMPPipelineScannerOptions : ScannerBaseOptions
{

}

