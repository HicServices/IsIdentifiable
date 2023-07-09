using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public sealed class YamlOptions
{
    [YamlMember(Alias = YamlOptionsConstants.IS_IDENTIFIABLE_YAML_KEY)]
    public IsIdentifiableOptions? IsIdentifiableOptions { get; init; }
}
