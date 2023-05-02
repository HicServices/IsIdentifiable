using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Rules
{
    /// <summary>
    /// Helper logic for <see cref="Rules"/> classes
    /// </summary>
    public static class RuleHelpers
    {
        /// <summary>
        /// Serializes the <paramref name="rule"/> into yaml
        /// </summary>
        /// <param name="rule"></param>
        /// <returns></returns>
        public static string Serialize(IRegexRule rule)
        {
            return GetSerializer().Serialize(new List<IRegexRule> { rule });
        }

        /// <summary>
        /// Serializes the <paramref name="rule"/> into yaml with a comment at the start
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string SerializeWithComment(IRegexRule rule, DateTime dateTime)
        {
            var yaml = GetSerializer().Serialize(new List<IRegexRule> { rule });

            return $"#{Environment.UserName} - {dateTime}{Environment.NewLine}{yaml}";
        }

        private static ISerializer GetSerializer() =>
            new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();
    }
}
