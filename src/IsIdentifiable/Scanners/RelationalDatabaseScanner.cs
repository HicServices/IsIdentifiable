using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Util;
using System;
using System.Data;
using System.Data.Common;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Scanners;

/// <summary>
/// IsIdentifiable runner which pulls data from a relational database 
/// table and evaluates it for identifiable information
/// </summary>
public class RelationalDatabaseScanner : ResourceScannerBase
{
    private readonly DbConnection _dbConnection;
    private readonly TopXResponse? _topXResponse;
    private readonly DiscoveredDatabase _database;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <param name="fileSystem"></param>
    /// <param name="stopAfter"></param>
    /// <exception cref="ArgumentException"></exception>
    public RelationalDatabaseScanner(
        RelationalDatabaseScannerOptions options,
        DatabaseTargetOptions databaseTargetOptions,
        IFileSystem fileSystem,
        int stopAfter,
        params IFailureReport[] reports
    )
        : base(options, fileSystem, reports)
    {
        _database = DatabaseTargetHelpers.GetDiscoveredDatabase(databaseTargetOptions);
        _dbConnection = _database.Server.GetConnection();
        _dbConnection.Open();
        _topXResponse = stopAfter > 0 ? _database.Server.GetQuerySyntaxHelper().HowDoWeAchieveTopX(stopAfter) : null;
    }

    /// <summary>
    /// Connects to the database server and fetches data from the remote table.  All
    /// records fetched are evaluated for identifiable data
    /// </summary>
    public void Scan(string tableName)
    {
        var table = DatabaseTargetHelpers.GetDiscoveredTable(_database, tableName);
        var columns = table.DiscoverColumns();
        var columnNames = columns.Select(c => c.GetRuntimeName()).ToArray();
        var stringColumns = columns.Select(c => c.GetGuesser().Guess.CSharpType == typeof(string)).ToArray();
        var primaryKeys = columns.Where(c => c.IsPrimaryKey).ToArray();

        // Assembles command 'SELECT TOP x a,b,c from Tbl' or for MySql/Oracle 'SELECT a,b,c from Tbl LIMIT x'
        var dbCommand = _database.Server.GetCommand(
            $@"SELECT
{(_topXResponse is { Location: QueryComponent.SELECT } ? _topXResponse.SQL : "")}
{string.Join($",{Environment.NewLine}", columns.Select(c => c.GetFullyQualifiedName()))}
FROM 
{tableName}
{(_topXResponse is { Location: QueryComponent.Postfix } ? _topXResponse.SQL : "")}",
            _dbConnection
        );

        Logger.Info($"About to send command:{Environment.NewLine}{dbCommand.CommandText}");
        using var reader = dbCommand.ExecuteReader();

        foreach (var record in reader.Cast<DbDataRecord>())
        {
            //Get the primary key of the current row
            var primaryKey = primaryKeys.Select(k => record[k.GetRuntimeName()].ToString()).Single();

            //For each column in the table
            for (var i = 0; i < columnNames.Length; i++)
            {
                //If it is not a string column
                if (!stringColumns[i])
                    continue;

                var asString = record[i] as string;

                if (string.IsNullOrWhiteSpace(asString))
                    continue;

                // Some strings contain null characters?!  Remove them all.
                // XXX hopefully this won't break any special character encoding (eg. UTF)
                var parts = asString.Split('\\').SelectMany(part => Validate(columnNames[i], part.Replace("\0", ""))).ToList();

                if (!parts.Any())
                    continue;

                NotifyNewFailure(new Failure(parts)
                {
                    Resource = tableName,
                    ResourcePrimaryKey = primaryKey,
                    ProblemField = columnNames[i],
                    ProblemValue = asString,
                });
            }

            NotifyDoneRows(1);
        }

        CloseReports();
    }

    protected override void DisposeImpl()
    {
        _dbConnection?.Dispose();
    }

    protected override string LogProgressNoun() => "records";
}
