using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using System.Collections.Generic;

namespace IsIdentifiable.Redacting.UpdateStrategies;

/// <summary>
/// builds SQL UPDATE statements based on the fixed strings in <see cref="FailurePart.Word"/>
/// </summary>
public class ProblemValuesUpdateStrategy : UpdateStrategy
{
    /// <summary>
    /// Generates 1 UPDATE statement per <see cref="Failure.Parts"/> for redacting the current <paramref name="failure"/>
    /// </summary>
    /// <param name="table"></param>
    /// <param name="primaryKeys"></param>
    /// <param name="failure"></param>
    /// <param name="usingRule"></param>
    /// <returns></returns>
    public override IEnumerable<string> GetUpdateSql(DiscoveredTable table,
        Dictionary<DiscoveredTable, DiscoveredColumn> primaryKeys, Failure failure, RegexRule usingRule)
    {
        var syntax = table.GetQuerySyntaxHelper();

        foreach (var part in failure.Parts)
        {

            yield return GetUpdateWordSql(table, primaryKeys, syntax, failure, part.Word);
        }
    }
}
