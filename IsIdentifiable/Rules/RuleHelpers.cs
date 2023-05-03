using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Rules
{
    /// <summary>
    /// Helper logic for <see cref="Rules"/> classes
    /// </summary>
    public static class RuleHelpers
    {
        /// <summary>
        /// Load a list of rules from the specified file
        /// </summary>
        /// <param name="rulesFile"></param>
        /// <param name="createIfMissing">If the specified file is not found, create it and return an empty list. Otherwise throws a <see cref="FileNotFoundException"/></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static IList<IRegexRule> LoadFrom(IFileInfo rulesFile, bool createIfMissing)
        {
            rulesFile.Refresh();

            if (!rulesFile.Exists)
            {
                if (!createIfMissing)
                    throw new FileNotFoundException($"{rulesFile.FullName} does not exist");

                rulesFile.Create();
                return new List<IRegexRule>();
            }

            var existingRules = rulesFile.FileSystem.File.ReadAllText(rulesFile.FullName);

            if (string.IsNullOrWhiteSpace(existingRules))
                return new List<IRegexRule>();

            var deserializer = new Deserializer();

            try
            {
                return deserializer.Deserialize<IList<RegexRule>>(existingRules).ToList<IRegexRule>();
            }
            catch (YamlException e)
            {
                throw new InvalidDataException($"Invalid rules content in file {rulesFile.FullName}", e);
            }
        }

        /// <summary>
        /// Serializes the <paramref name="rule"/> into yaml
        /// </summary>
        /// <param name="rule"></param>
        /// <returns></returns>
        public static string Serialize(IRegexRule rule)
        {
            return new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build()
                .Serialize(new List<IRegexRule> { rule });
        }

        /// <summary>
        /// Serializes the <paramref name="rule"/> into yaml with a comment at the start
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string SerializeWithComment(IRegexRule rule, DateTime dateTime)
        {
            var yaml = Serialize(rule);
            return $"#{Environment.UserName} - {dateTime}{Environment.NewLine}{yaml}";
        }
    }
}
