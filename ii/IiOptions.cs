using IsIdentifiable.Options;
using YamlDotNet.Serialization;

namespace ii;

internal class IiOptions : IsIdentifiableOptions
{
    [YamlMember(Alias = "Reviewer")]
    public ReviewerOptions? ReviewerOptions { get; init; }
}
