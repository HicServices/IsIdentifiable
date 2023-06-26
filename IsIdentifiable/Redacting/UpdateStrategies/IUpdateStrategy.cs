using System.Collections.Generic;
using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Rules;

namespace IsIdentifiable.Redacting.UpdateStrategies;

/// <summary>
/// Interface for generating SQL statements that perform redactions on a database for a given <see cref="Failure"/> when applying an <see cref="IRegexRule"/>
/// </summary>
public interface IUpdateStrategy
{
    /// <summary>
    /// Returns SQL that should be run on <paramref name="table"/> to redact the <paramref name="failure"/> by removing the parts matched in <paramref name="usingRule"/>
    /// </summary>
    /// <param name="table">Table on which the SQL should be run</param>
    /// <param name="primaryKeys">Cached primary key knowledge about all tables encountered so far, index with <paramref name="table"/> to determine primary keys</param>
    /// <param name="failure">The cell and value in which a problem value was detected</param>
    /// <param name="usingRule">How to redact the <see cref="Failure.ProblemValue"/></param>
    /// <returns></returns>
    IEnumerable<string> GetUpdateSql(DiscoveredTable table,
        Dictionary<DiscoveredTable, DiscoveredColumn> primaryKeys, Failure failure, IRegexRule usingRule);
}