using FAnsi;

namespace IsIdentifiable.Options;

public abstract class ScannerBaseOptions
{
    /// <summary>
    /// Set this to enable progress logging every N items (files, rows etc.).
    /// Defaults to <see cref="DEFAULT_LOG_PROGRESS_EVERY"/>
    /// </summary>
    public int LogProgressEvery { get; init; } = DEFAULT_LOG_PROGRESS_EVERY;

    /// <summary>
    /// Comma separated list of columns/tags which should be ignored during validation
    /// </summary>
    public string? SkipColumns { get; init; }

    /// <summary>
    /// Maximum number of answers to cache per column.
    /// Defaults to <see cref="DEFAULT_VALIDATION_CACHE_LIMIT"/>
    /// </summary>
    public int ValidationCacheLimit { get; init; } = DEFAULT_VALIDATION_CACHE_LIMIT;

    /// <summary>
    /// Filename containing validation rules.
    /// Mutually exclusive with <see cref="RulesDirectory"/>
    /// </summary>
    public string? RulesFile { get; init; }

    /// <summary>
    /// Directory of rules to load.
    /// Mutually exclusive with <see cref="RulesFile"/>
    /// </summary>
    public string? RulesDirectory { get; init; }

    /// <summary>
    /// Path to a CSV file containing a single untitled column of Allowlist values
    /// </summary>
    public string? AllowListFile { get; init; }

    /// <summary>
    /// Full connection string to the database storing the Allowlist of valid entries
    /// </summary>
    public string? AllowListConnectionString { get; init; }

    /// <summary>
    /// The DBMS provider of the Allowlist table e.g. MySql
    /// </summary>
    public DatabaseType? AllowListDatabaseType { get; init; }

    /// <summary>
    /// The unqualified name of the Allowlist table
    /// </summary>
    public string? AllowListTableName { get; init; }

    /// <summary>
    /// The column in AllowListTableName which contains the Allowlist elements
    /// </summary>
    public string? AllowListColumn { get; init; }

    /// <summary>
    /// If specified postcodes will not be reported as failures
    /// </summary>
    public bool IgnorePostcodes { get; init; } = false;

    /// <summary>
    /// If set and using a 7 class NER model then DATE and TIME objects will not be considered failures
    /// </summary>
    public bool IgnoreDatesInText { get; init; } = false;


    private const int DEFAULT_LOG_PROGRESS_EVERY = 1000;
    private const int DEFAULT_VALIDATION_CACHE_LIMIT = 1_000_000;
}
