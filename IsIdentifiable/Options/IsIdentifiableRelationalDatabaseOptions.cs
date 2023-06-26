using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using FAnsi;
using IsIdentifiable.Redacting;
using NLog;

namespace IsIdentifiable.Options;

/// <summary>
/// Options class for runnning IsIdentifiable on a table in a relational database
/// </summary>
[Verb("db", HelpText = "Run tool on data held in a relational database table")]
public class IsIdentifiableRelationalDatabaseOptions : IsIdentifiableBaseOptions
{
    /// <summary>
    /// Full connection string to the database storing the table to be evaluated
    /// </summary>
    [Option('d', HelpText = "Full connection string to the database storing the table to be evaluated", Required = true)]
    public string DatabaseConnectionString { get; set; }

    /// <summary>
    /// The unqualified name of the table to evaluate e.g. "My Cool Table"
    /// </summary>
    [Option('t', HelpText = "The unqualified name of the table to evaluate", Required = true)]
    public string TableName { get; set; }

    /// <summary>
    /// The type of Relational database
    /// </summary>
    [Option('p', HelpText = "DBMS Provider type - 'MicrosoftSQLServer','MySql', 'PostgreSql' or 'Oracle'", Required = true)]
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// Examples for running IsIdentifiable command line against a table in a database
    /// </summary>
    [Usage]
    public static IEnumerable<Example> Examples
    {
        get
        {
            yield return new Example("Run on a MySql database", new IsIdentifiableRelationalDatabaseOptions
            {
                DatabaseConnectionString = "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;",
                DatabaseType = DatabaseType.MySql,
                TableName = "MyTable",
                StoreReport = true

            });
            yield return new Example(
                "Run on an Sql Server database",
                new IsIdentifiableRelationalDatabaseOptions
                {
                    DatabaseConnectionString = "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;",
                    DatabaseType = DatabaseType.MicrosoftSQLServer,
                    TableName = "MyTable",
                    StoreReport = true
                });
        }
    }

    /// <summary>
    /// Returns the <see cref="TableName"/> for use in output report names
    /// </summary>
    /// <returns></returns>
    public override string GetTargetName(IFileSystem _)
    {
        return TableName;
    }

    /// <summary>
    /// Redacts all base class connection strings and <see cref="DatabaseConnectionString"/> to use
    /// named servers if specified
    /// </summary>
    /// <param name="targets"></param>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public override int UpdateConnectionStringsToUseTargets(out List<Target> targets, IFileSystem fileSystem)
    {
        var logger = LogManager.GetCurrentClassLogger();
        var result = base.UpdateConnectionStringsToUseTargets(out targets, fileSystem);

        if (result != 0)
            return result;


        // see if user passed the name of a target for DatabaseConnectionString (server to query for data)
        var db = targets.FirstOrDefault(t => string.Equals(t?.Name, this.DatabaseConnectionString, StringComparison.CurrentCultureIgnoreCase));
        if (db != null)
        {
            logger.Info($"Using named target for {nameof(this.DatabaseConnectionString)}");
            this.DatabaseConnectionString = db.ConnectionString;
            this.DatabaseType = db.DatabaseType;
        }

        return 0;
    }
}