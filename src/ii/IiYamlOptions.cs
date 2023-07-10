using IsIdentifiable.Options;
using System;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace ii;

internal class IiYamlOptions
{
    [YamlMember(Alias = YamlOptionsConstants.IS_IDENTIFIABLE_YAML_KEY)]
    public IiOptions? IsIdentifiableOptions { get; init; }

    public static IiOptions LoadFrom(IFileInfo fileInfo)
    {
        var yamlOptions = YamlOptionsExtensions.Load<IiYamlOptions>(fileInfo);
        return yamlOptions?.IsIdentifiableOptions ?? throw new ArgumentException($"Could not find key '{YamlOptionsConstants.IS_IDENTIFIABLE_YAML_KEY}' in file");
    }
}
