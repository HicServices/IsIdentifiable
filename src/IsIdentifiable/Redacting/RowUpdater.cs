using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Redacting.UpdateStrategies;
using IsIdentifiable.Rules;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Redacting;

/// <summary>
/// <para>
/// Implementation of OutBase for <see cref="RuleAction.Report"/>.  Base class <see cref="OutBase.Rules"/> should
/// be interpreted as rules for detecting <see cref="Failure"/> which should be redacted.  This involves adding a new rule
/// to redact the failure.  Then if not in <see cref="RulesOnly"/> updating the database to perform the redaction.
/// </para>
/// <para>See also:<seealso cref="IgnoreRuleGenerator"/></para>
/// </summary>
public class RowUpdater : OutBase
{
    /// <summary>
    /// Default name for the true positive detection rules (for redacting with).  This file will be appended to as new rules are added.
    /// </summary>
    public const string DefaultFileName = "Reportlist.yaml";

    /// <summary>
    /// Set to true to only output updates to Reportlist instead of trying to update the database.
    /// This is useful if you want to  run in manual mode to process everything then run unattended
    /// for the updates.
    /// </summary>
    public bool RulesOnly { get; set; }

    readonly Dictionary<DiscoveredTable, DiscoveredColumn> _primaryKeys = new();

    /// <summary>
    /// The strategy to use to build SQL updates to run on the database
    /// </summary>
    public IUpdateStrategy UpdateStrategy = new RegexUpdateStrategy();

    /// <summary>
    /// Creates a new instance which stores rules in the <paramref name="rulesFile"/> (which will also have existing rules loaded from)
    /// </summary>
    public RowUpdater(IFileSystem fileSystem, IFileInfo rulesFile = null)
        : base(rulesFile, fileSystem, DefaultFileName)
    { }

    /// <summary>
    /// Update the database <paramref name="server"/> to redact the <paramref name="failure"/>.
    /// </summary>
    /// <param name="server">Where to connect to get the data, can be null if <see cref="RulesOnly"/> is true</param>
    /// <param name="failure">The failure to redact/create a rule for</param>
    /// <param name="usingRule">Pass null to create a new rule or give value to reuse an existing rule</param>
    public void Update(DiscoveredServer server, Failure failure, RegexRule usingRule)
    {
        //there's no rule yet so create one (and add to Reportlist.yaml)
        usingRule ??= Add(failure, RuleAction.Report);

        //if we are running in rules only mode we don't need to also update the database
        if (RulesOnly)
            return;
        // Server can only be null if we are running in RulesOnly mode
        ArgumentNullException.ThrowIfNull(server);

        var syntax = server.GetQuerySyntaxHelper();

        //the fully specified name e.g. [mydb]..[mytbl]
        var tableName = failure.Resource;

        var tokens = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        var db = tokens.First();
        tableName = tokens.Last();

        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(tableName) || string.Equals(db, tableName))
            throw new NotSupportedException($"Could not understand table name {failure.Resource}, maybe it is not full specified with a valid database and table name?");

        db = syntax.GetRuntimeName(db);
        tableName = syntax.GetRuntimeName(tableName);

        var table = server.ExpectDatabase(db).ExpectTable(tableName);

        //if we've never seen this table before
        if (!_primaryKeys.ContainsKey(table))
        {
            var pk = table.DiscoverColumns().SingleOrDefault(k => k.IsPrimaryKey);
            _primaryKeys.Add(table, pk);
        }

        using var con = server.GetConnection();
        con.Open();

        foreach (var cmd in UpdateStrategy.GetUpdateSql(table, _primaryKeys, failure, usingRule)
                     .Select(sql => server.GetCommand(sql, con)))
            cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Adds a rule to update the given failure without sending any update instructions to the server
    /// </summary>
    /// <param name="f"></param>
    public void Add(Failure f)
    {
        Add(f, RuleAction.Report);
    }

    /// <summary>
    /// Handler for loading <paramref name="failure"/>.  If the user previously made an update decision an
    /// update will transparently happen for this record and false is returned.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="failure"></param>
    /// <param name="rule">The first rule that covered the <paramref name="failure"/></param>
    /// <returns>True if <paramref name="failure"/> is novel and not seen before</returns>
    public bool OnLoad(DiscoveredServer? server, Failure failure, out RegexRule rule)
    {
        // if we are not updating the server just tell them if it is novel or not
        if (server == null)
            return !IsCoveredByExistingRule(failure, out rule);

        //if we have seen this before
        if (IsCoveredByExistingRule(failure, out rule))
        {
            //since user has issued an update for this exact problem before we can update this one too
            Update(server, failure, rule);

            //and return false to indicate that it is not a novel issue
            return false;
        }

        return true;
    }
}
