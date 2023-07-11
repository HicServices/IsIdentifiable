using IsIdentifiable.Rules;
using System;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace ii.Review.Views.Manager;

internal class RuleSetFileNode
{
    public IFileInfo File { get; init; }

    /// <summary>
    /// The ruleset in the <see cref="File"/>.  It is important to populate this only once
    /// otherwise children generated from this class will end up with different collections
    /// over time.
    /// </summary>
    private RuleSet? _ruleSet;

    public RuleSetFileNode(IFileInfo file)
    {
        File = file;
    }

    /// <summary>
    /// Opens the ruleset file and reads all rules. This caches 
    /// </summary>
    /// <returns></returns>
    public RuleSet GetRuleSet()
    {
        if (_ruleSet != null)
            return _ruleSet;

        try
        {
            _ruleSet = RuleSet.LoadFrom(File);
        }
        catch (Exception)
        {
            _ruleSet = new RuleSet();
        }

        return _ruleSet;
    }

    public void Save(IFileInfo? toFile = null)
    {
        // TODO(rkm 2023-07-11) Refactor using RuleStore

        if (_ruleSet == null)
            throw new Exception("Cannot save before children have been created, expected GetRuleSet() to have been called before now");

        toFile ??= File;

        //populated rules file already existed
        var builder = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .WithIndentedSequences();

        var serializer = builder.Build();
        using var sw = new System.IO.StreamWriter(toFile.FullName);
        serializer.Serialize(sw, _ruleSet);
    }

    public override string ToString() => File.Name;
}
