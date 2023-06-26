using CommandLine;
using IsIdentifiable.Reporting.Reports;
using System;

namespace IsIdentifiable.Options;

/// <summary>
/// CLI options for the reviewer
/// </summary>
[Verb("review", HelpText = "Review or redact the StoreReport output of an IsIdentifiable run")]
public class IsIdentifiableReviewerOptions
{
    /// <summary>
    /// Default value for <see cref="TargetsFile"/>
    /// </summary>
    public const string TargetsFileDefault = "Targets.yaml";

    /// <summary>
    /// The CSV list of failures to process.  Must be in the format of a <see cref="FailureStoreReport"/>
    /// </summary>
    [Option('f', "file",
        Required = false,
        HelpText = "[Optional] Pre load an existing failures file"
    )]
    public string FailuresCsv { get; set; }

    /// <summary>
    /// The output path to put unredacted but unignored results (did not match any 'review stage' rules)
    /// </summary>
    [Option('u', "unattended",
        Required = false,
        HelpText = "[Optional] Runs the application automatically processing existing update/ignore rules.  Failures not matching either are written to a new file with this path"
    )]
    public string UnattendedOutputPath { get; set; }
      
    /// <summary>
    /// File containing rules for ignoring PII during redaction
    /// </summary>
    [Option('i', "ignore",
        Required = false,
        Default = "NewRules.yaml",
        HelpText = "File containing rules for ignoring validation errors"
    )]
    public string IgnoreList { get; set; }

    /// <summary>
    /// _rules for identifying which sub parts of a PII match should be redacted
    /// </summary>
    [Option('r', "Reportlist",
        Required = false,
        Default = "ReportList.yaml",
        HelpText = "File containing rules for when to issue UPDATE statements"
    )]
    public string Reportlist { get; set; }

    /// <summary>
    /// Sets UseSystemConsole to true for Terminal.gui (i.e. uses the NetDriver which is based on System.Console)
    /// </summary>
    [Option("usc", HelpText = "Sets UseSystemConsole to true for Terminal.gui (i.e. uses the NetDriver which is based on System.Console)")]
    public bool UseSystemConsole { get; internal set; }

    /// <summary>
    /// Sets the user interface to use a specific color palette yaml file
    /// </summary>
    [Option("theme", HelpText = "Sets the user interface to use a specific color palette yaml file")]
    public string Theme { get; set; }


    /// <summary>
    /// Populates values in this instance where no value yet exists and there is a value in <paramref name="globalOpts"/>
    /// to inherit.
    /// </summary>
    /// <param name="globalOpts"></param>
    public virtual void InheritValuesFrom(IsIdentifiableReviewerOptions globalOpts)
    {
        if ((string.IsNullOrWhiteSpace(IgnoreList) || IgnoreList == IgnoreRuleGenerator.DefaultFileName) &&
            !string.IsNullOrWhiteSpace(globalOpts.IgnoreList))
            IgnoreList = globalOpts.IgnoreList;

        if ((string.IsNullOrWhiteSpace(Reportlist) || Reportlist == RowUpdater.DefaultFileName) &&
            !string.IsNullOrWhiteSpace(globalOpts.Reportlist))
            Reportlist = globalOpts.Reportlist;

        if (Theme == null && !string.IsNullOrWhiteSpace(globalOpts.Theme))
            Theme = globalOpts.Theme;
    }
}
