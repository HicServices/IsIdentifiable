using CommandLine;
using IsIdentifiable.Reporting.Reports;
using System;

namespace IsIdentifiable.Options;

[Obsolete("TODO(rkm 2023-07-05) Move to ii project")]
[Verb("review", HelpText = "Review or redact the StoreReport output of an IsIdentifiable run")]
public class IsIdentifiableReviewerOptions
{
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
}
