using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public static class YamlOptionsExtensions
{
    public static T? Load<T>(IFileInfo fileInfo) =>
        new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build()
        .Deserialize<T>(
            fileInfo.OpenText().ReadToEnd()
        );
}
