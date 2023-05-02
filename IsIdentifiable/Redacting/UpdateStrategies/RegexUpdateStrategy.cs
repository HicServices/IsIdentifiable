using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FAnsi.Discovery;
using IsIdentifiable.Reporting;
using IsIdentifiable.Rules;

namespace IsIdentifiable.Redacting.UpdateStrategies;

/// <summary>
/// builds SQL UPDATE statements based on the capture groups of <see cref="IRegexRule.IfPattern"/> when matching
/// <see cref="Failure.ProblemValue"/>.  Where this is unclear (e.g. no capture groups in Regex) or no rule is available
/// then falls back on <see cref="ProblemValuesUpdateStrategy"/>
/// </summary>
public class RegexUpdateStrategy : UpdateStrategy
{
    private readonly ProblemValuesUpdateStrategy _fallback = new();

    /// <summary>
    /// Returns SQL for updating the <paramref name="table"/> to redact the capture groups in <see cref="IRegexRule.IfPattern"/>.  If no capture groups are represented in the <paramref name="usingRule"/> then this class falls back on <see cref="ProblemValuesUpdateStrategy"/>
    /// </summary>
    /// <param name="table"></param>
    /// <param name="primaryKeys"></param>
    /// <param name="failure"></param>
    /// <param name="usingRule"></param>
    /// <returns></returns>
    public override IEnumerable<string> GetUpdateSql(DiscoveredTable table, Dictionary<DiscoveredTable, DiscoveredColumn> primaryKeys, Failure failure, IRegexRule usingRule)
    {
        if (usingRule == null || string.IsNullOrWhiteSpace(usingRule.IfPattern))
            return _fallback.GetUpdateSql(table, primaryKeys, failure, usingRule);

        try
        {
            var r = new Regex(usingRule.IfPattern);
            var match = r.Match(failure.ProblemValue);

            //Group 1 (index 0) is always the full match, we want selective updates
            if (match.Success && match.Groups.Count > 1)
            {
                var syntax = table.GetQuerySyntaxHelper();

                //update the capture groups of the Regex
                return match.Groups.Cast<Group>().Skip(1).Select(m => GetUpdateWordSql(table, primaryKeys, syntax, failure, m.Value));
            }

            //The Regex did not have capture groups or did not match the failure
            return _fallback.GetUpdateSql(table, primaryKeys, failure, usingRule);

        }
        catch (Exception)
        {
            //The Regex pattern was bad or something else bad went wrong
            return _fallback.GetUpdateSql(table, primaryKeys, failure, usingRule);
        }
    }
}