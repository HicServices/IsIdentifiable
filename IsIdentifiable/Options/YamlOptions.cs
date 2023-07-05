using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public sealed class YamlOptions
{
    public const string IS_IDENTIFIABLE_YAML_KEY = "IsIdentifiable";

    [YamlMember(Alias = IS_IDENTIFIABLE_YAML_KEY)]
    public IsIdentifiableOptions? IsIdentifiableOptions { get; init; }
}
