using YamlDotNet.Serialization;

namespace IsIdentifiable.Rules;

public static class RuleHelpers
{
    public static IDeserializer GetRuleDeserializer()
    {
        var builder = new DeserializerBuilder();
        builder.WithTagMapping("!SocketRule", typeof(SocketRule));
        builder.WithTagMapping("!AllowlistRule", typeof(AllowlistRule));
        builder.WithTagMapping("!RegexRule", typeof(RegexRule));
        return builder.Build();
    }
}
