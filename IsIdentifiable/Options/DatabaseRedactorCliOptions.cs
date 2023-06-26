using CommandLine;

namespace IsIdentifiable.Options
{
    public class DatabaseRedactorCliOptions
    {
        /// <summary>
        /// Path to the CSV file of failures to process
        /// </summary>
        [Option(
            'f',
            "failures-file",
            Required = true,
            HelpText = "Path to the CSV file of failures to process. Must be in the format of a FailureStoreReport"
        )]
        public string FailuresCsv { get; set; }
    }
}
