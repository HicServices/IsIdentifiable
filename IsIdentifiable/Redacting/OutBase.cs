using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using IsIdentifiable.Failures;
using IsIdentifiable.Reporting;
using IsIdentifiable.Rules;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Redacting;

/// <summary>
/// Abstract base for classes who act upon <see cref="Failure"/> by creating new <see cref="Rules"/> and/or redacting the database.
/// </summary>
public abstract class OutBase
{
    /// <summary>
    /// FileSystem to use for I/O
    /// </summary>
    protected IFileSystem FileSystem;

    /// <summary>
    /// Existing rules which describe how to detect a <see cref="Failure"/> that should be handled by this class.  These are synced with the contents of the <see cref="RulesFile"/>
    /// </summary>
    public List<IRegexRule> Rules { get; }

    /// <summary>
    /// Persistence of <see cref="RulesFile"/>
    /// </summary>
    public IFileInfo RulesFile { get; }

    /// <summary>
    /// Factory for creating new <see cref="Rules"/> when encountering novel <see cref="Failure"/> that do not match any existing rules.  May involve user input.
    /// </summary>
    public IRulePatternFactory RulesFactory { get; set; } = new MatchWholeStringRulePatternFactory();

    /// <summary>
    /// Record of changes to <see cref="Rules"/> (and <see cref="RulesFile"/>).
    /// </summary>
    public Stack<OutBaseHistory> History = new();

    /// <summary>
    /// Creates a new instance, populating <see cref="Rules"/> with the files serialized in <paramref name="rulesFile"/>
    /// </summary>
    /// <param name="rulesFile">Location to load/persist rules from/to.  Will be created if it does not exist yet</param>
    /// <param name="fileSystem"></param>
    /// <param name="defaultFileName"></param>
    protected OutBase(IFileInfo rulesFile, IFileSystem fileSystem, string defaultFileName)
    {
        FileSystem = fileSystem;
        rulesFile ??= FileSystem.FileInfo.New(defaultFileName);

        RulesFile = rulesFile;

        //no rules file yet
        if (!FileSystem.File.Exists(rulesFile.FullName))
        {
            //create it as an empty file
            using (rulesFile.Create())
                Rules = new List<IRegexRule>();
        }
        else
        {
            var existingRules = FileSystem.File.ReadAllText(rulesFile.FullName);

            //empty rules file
            if (string.IsNullOrWhiteSpace(existingRules))
                Rules = new List<IRegexRule>();
            else
            {
                //populated rules file already existed
                var deserializer = new Deserializer();

                try
                {
                    Rules = deserializer.Deserialize<List<RegexRule>>(existingRules).ToList<IRegexRule>() ?? new List<IRegexRule>();

                }
                catch (YamlException e)
                {
                    Console.Error.WriteLine($"Error desieralizing {rulesFile}: {e}");
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Adds a new rule (both to the <see cref="RulesFile"/> and the in memory <see cref="Rules"/> collection).
    /// </summary>
    /// <param name="f"></param>
    /// <param name="action"></param>
    /// <param name="overrideRuleFactory">Overrides the current <see cref="RulesFactory"/> and uses this instead</param>
    /// <returns>The new / existing rule that covers failure</returns>
    protected IRegexRule Add(Failure f, RuleAction action, IRulePatternFactory overrideRuleFactory = null)
    {
        var factory = overrideRuleFactory ?? RulesFactory;

        var rule = new RegexRule
        {
            Action = action,
            IfColumn = f.ProblemField,
            IfPattern = factory.GetPattern(this, f),
            As =
                action == RuleAction.Ignore ?
                    FailureClassification.None :
                    f.Parts.Select(p => p.Classification).FirstOrDefault()
        };

        return Add(rule);
    }

    /// <summary>
    /// Serializes <paramref name="rule"/> into the rules base if it is novel (not identical to any
    /// other rules.
    /// </summary>
    /// <param name="rule"></param>
    /// <returns>The <paramref name="rule"/> passed or the existing identical rule if one already exists
    /// in rules base</returns>
    public IRegexRule Add(IRegexRule rule)
    {
        //don't add identical rules
        if (Rules.Any(r => r.AreIdentical(rule)))
            return rule;

        Rules.Add(rule);

        var contents = Serialize(rule, true);

        FileSystem.File.AppendAllText(RulesFile.FullName, contents);
        History.Push(new OutBaseHistory(rule, contents));

        return rule;
    }

    /// <summary>
    /// Serializes the <paramref name="rule"/> into yaml optionally with a comment at the start
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="addCreatorComment"></param>
    /// <returns></returns>
    private string Serialize(IRegexRule rule, bool addCreatorComment)
    {
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        var yaml = serializer.Serialize(new List<IRegexRule> { rule });

        if (!addCreatorComment)
            return yaml;

        return $"#{Environment.UserName} - {DateTime.Now}{Environment.NewLine}{yaml}";
    }

    /// <summary>
    /// Removes an existing rule and flushes out to disk
    /// </summary>
    /// <param name="rule"></param>
    /// <returns>True if the rule existed and was successfully deleted in memory and on disk</returns>
    public bool Delete(IRegexRule rule)
    {
        return Rules.Remove(rule) && Purge(Serialize(rule, false), $"# Rule deleted by {Environment.UserName} - {DateTime.Now}{Environment.NewLine}");
    }

    /// <summary>
    /// Removes the last <see cref="History"/> entry from the <see cref="Rules"/> and <see cref="RulesFile"/>.
    /// </summary>
    public void Undo()
    {
        if (History.Count == 0)
            return;

        var popped = History.Pop();

        if (popped != null)
        {
            Purge(popped.Yaml);

            //clear the rule from memory
            Rules.Remove(popped.Rule);
        }
    }

    /// <summary>
    /// Serializes the current <see cref="Rules"/> to the provided file
    /// </summary>
    /// <param name="toFile"></param>
    public void Save(IFileInfo toFile = null)
    {
        toFile ??= RulesFile;

        toFile.Delete();

        var builder = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .WithIndentedSequences();

        var serializer = builder.Build();
        using var stream = toFile.OpenWrite();
        using var sw = new System.IO.StreamWriter(stream);
        if (Rules.Count > 0)
        {
            serializer.Serialize(sw, Rules);
        }
        // else we get a blank file which is good because the append rules wouldn't play nice
        // with an [] deal
    }

    /// <summary>
    /// Attempts to purge the provided block of serialized rules yaml from the rules base on disk
    /// </summary>
    /// <param name="yaml"></param>
    /// <param name="replacement"></param>
    /// <returns></returns>
    private bool Purge(string yaml, string replacement = "")
    {
        //clear the rule from the serialized text file
        var oldText = FileSystem.File.ReadAllText(RulesFile.FullName);
        var newText = oldText.Replace(yaml, replacement);

        if (Equals(oldText, newText))
            return false;

        var temp = $"{RulesFile.FullName}.tmp";

        //write to a new temp file
        FileSystem.File.WriteAllText(temp, newText);

        //then hot swap them
        FileSystem.File.Copy(temp, RulesFile.FullName,true);
        FileSystem.File.Delete(temp);

        return true;
    }

    /// <summary>
    /// Returns true if there are any rules that already exactly cover the given <paramref name="failure"/>
    /// </summary>
    /// <param name="failure"></param>
    /// <param name="match">The first rule that matches the <paramref name="failure"/></param>
    /// <returns></returns>
    protected bool IsCoveredByExistingRule(Failure failure, out IRegexRule match)
    {
        //if any rule matches then we are covered by an existing rule
        match = Rules.FirstOrDefault(r => r.Apply(failure.ProblemField, failure.ProblemValue, out _) != RuleAction.None);
        return match != null;
    }
}
