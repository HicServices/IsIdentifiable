using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Runners;

/// <summary>
/// IsIdentifiable runner which pulls data from a relational database 
/// table and evaluates it for identifiable information
/// </summary>
public class DatabaseRunner : RunnerBase
{
    private readonly IsIdentifiableRelationalDatabaseOptions _opts;
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    private string _tableName;
    private DiscoveredColumn[] _columns;
    private string[] _columnsNames;
    private bool[] _stringColumns;
    private DiscoveredColumn[] _primaryKeys;

    /// <summary>
    /// Creates a new instance that will read from the 
    /// listed <see cref="IsIdentifiableRelationalDatabaseOptions.DatabaseConnectionString"/>
    /// in <paramref name="opts"/>
    /// </summary>
    /// <param name="opts">Database to read from and rules settings</param>
    /// <param name="fileSystem"></param>
    public DatabaseRunner(IsIdentifiableRelationalDatabaseOptions opts, IFileSystem fileSystem)
        : base(opts, fileSystem)
    {
        _opts = opts;
        LogProgressNoun = "records";
    }

    /// <summary>
    /// Connects to the database server and fetches data from the remote table.  All
    /// records fetched are evaluated for identifiable data
    /// </summary>
    /// <returns>0 if all went well</returns>
    public override int Run()
    {
        var table = GetServer(_opts.DatabaseConnectionString, _opts.DatabaseType, _opts.TableName);
        _tableName = table.GetFullyQualifiedName();

        var server = table.Database.Server;

        _columns = table.DiscoverColumns();
        _columnsNames = _columns.Select(c => c.GetRuntimeName()).ToArray();
        _stringColumns = _columns.Select(c => c.GetGuesser().Guess.CSharpType == typeof(string)).ToArray();
        _primaryKeys = _columns.Where(c => c.IsPrimaryKey).ToArray();

        using var con = server.GetConnection();
        con.Open();

        var top = _opts.Top > 0 ? server.GetQuerySyntaxHelper().HowDoWeAchieveTopX(_opts.Top) : null;

        // assembles command 'SELECT TOP x a,b,c from Tbl'
        // or for MySql/Oracle 'SELECT a,b,c from Tbl LIMIT x'

        var cmd = server.GetCommand(
            $@"SELECT 
{(top is { Location: QueryComponent.SELECT } ? top.SQL : "")}
{string.Join($",{Environment.NewLine}", _columns.Select(c => c.GetFullyQualifiedName()))}
FROM 
{_tableName}
{(top is { Location: QueryComponent.Postfix } ? top.SQL : "")}"
            , con);

        _logger.Info($"About to send command:{Environment.NewLine}{cmd.CommandText}");

        using var reader = cmd.ExecuteReader();

        foreach (var failure in reader.Cast<DbDataRecord>().SelectMany(GetFailures))
            AddToReports(failure);

        CloseReports();

        return 0;
    }

    private IEnumerable<Failure> GetFailures(DbDataRecord record)
    {
        //Get the primary key of the current row
        var primaryKey = _primaryKeys.Select(k => record[k.GetRuntimeName()].ToString()).Single();

        //For each column in the table
        for (var i = 0; i < _columnsNames.Length; i++)
        {
            //If it is not a string column
            if (!_stringColumns[i])
                continue;

            var asString = record[i] as string;

            if (string.IsNullOrWhiteSpace(asString))
                continue;

            // Some strings contain null characters?!  Remove them all.
            // XXX hopefully this won't break any special character encoding (eg. UTF)
            var parts = asString.Split('\\').SelectMany(part => Validate(_columnsNames[i], part.Replace("\0", ""))).ToList();

            if (!parts.Any())
                continue;

            yield return new Failure(parts)
            {
                Resource = _tableName,
                ResourcePrimaryKey = primaryKey,
                ProblemField = _columnsNames[i],
                ProblemValue = asString,
            };
        }

        DoneRows(1);
    }
}
