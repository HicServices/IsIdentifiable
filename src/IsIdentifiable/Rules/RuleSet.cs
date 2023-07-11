using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Rules;

/// <summary>
/// A collection of rules which tell IsIdentifiable how to classify 
/// identifiable data in strings. Includes support for remote services
/// (e.g. NLP) and allow lists (ignore false positives)
/// </summary>
public class RuleSet
{
    /// <summary>
    /// Normal rules e.g. regular expressions for identifying/ignoring bits of text discovered
    /// </summary>
    public List<RegexRule> BasicRules { get; set; } = new();

    /// <summary>
    /// Remote services that expand IsIdentifiables base capabilities e.g. NLP services such as
    /// spaCy
    /// </summary>
    public List<SocketRule> SocketRules { get; set; } = new();

    /// <summary>
    /// Rules which suppress other rules.  If another rule (e.g. NLP) classifies the data is
    /// identifiable but it matches an <see cref="AllowlistRule"/> then it will not be reported
    /// and instead suppressed (as a false positive).
    /// </summary>
    public List<AllowlistRule> AllowlistRules { get; set; } = new();

    /// <summary>
    /// Rules which combine one or more other rules to get a 'consensus' (or INTERSECT).
    /// This can be used to deal with over matching in NLP e.g. by running 2 different 
    /// NLP classifiers and only reporting identifiable data when both agree.
    /// </summary>
    public List<ConsensusRule> ConsensusRules { get; set; } = new();

    /// <summary>
    /// Loads a <see cref="RuleSet"/> from the specified <paramref name="yamlFile"/>
    /// </summary>
    /// <param name="yamlFile"></param>
    /// <returns></returns>
    public static RuleSet LoadFrom(IFileInfo yamlFile)
    {
        var yamlContent = yamlFile.FileSystem.File.ReadAllText(yamlFile.FullName);

        var builder = new DeserializerBuilder();
        builder.WithTagMapping("!SocketRule", typeof(SocketRule));
        builder.WithTagMapping("!AllowlistRule", typeof(AllowlistRule));
        builder.WithTagMapping("!RegexRule", typeof(RegexRule));
        var deserializer = builder.Build();

        return deserializer.Deserialize<RuleSet>(yamlContent) ?? throw new ArgumentException($"Specififed file did not contain any rules");
    }
}
