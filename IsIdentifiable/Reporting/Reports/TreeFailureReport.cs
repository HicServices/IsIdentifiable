using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Reporting.Reports;

/// <summary>
/// Failure report for items from tree-like data such as MongoDB documents.
/// </summary>
internal class TreeFailureReport : FailureReport
{
    private const int TOTAL_SEEN_IDX = 0;
    private const int TOTAL_FAILED_IDX = 1;

    private readonly string[] _headerRow = { "Node", "TotalSeen", "TotalFailed", "PercentFailed" };

    private readonly object _nodeFailuresLock = new();

    // <NodePath, [TotalSeen, TotalFailed]>
    private readonly SortedDictionary<string, int[]> _nodeFailures = new();

    private readonly bool _reportAggregateCounts;

    public TreeFailureReport(string targetName, IFileSystem fileSystem, bool reportAggregateCounts = false)
        : base(targetName, fileSystem)
    {
        _reportAggregateCounts = reportAggregateCounts;
    }

    public override void AddDestinations(IsIdentifiableBaseOptions opts)
    {
        base.AddDestinations(opts);
        Destinations.ForEach(d => d.WriteHeader(_headerRow));
    }

    public override void Add(Failure failure)
    {
        IncrementCounts(failure.ProblemField, TOTAL_FAILED_IDX);
    }

    /// <summary>
    /// Record seen nodes and their counts
    /// </summary>
    /// <param name="nodeCounts"></param>
    public void AddNodeCounts(IDictionary<string, int> nodeCounts)
    {
        foreach (var kvp in nodeCounts)
            IncrementCounts(kvp.Key, TOTAL_SEEN_IDX, kvp.Value);
    }

    protected override void CloseReportBase()
    {
        using var dt = new DataTable();
        foreach (var col in _headerRow)
            dt.Columns.Add(col);

        if (_reportAggregateCounts)
            GenerateAggregateCounts();

        lock (_nodeFailuresLock)
            foreach (var item in _nodeFailures.Where(f => f.Value[TOTAL_SEEN_IDX] != 0))
            {
                var seen = item.Value[TOTAL_SEEN_IDX];
                var failed = item.Value[TOTAL_FAILED_IDX];

                dt.Rows.Add(item.Key, seen, failed, 100.0 * failed / seen);
            }

        foreach (var d in Destinations)
            d.WriteItems(dt);
    }

    private void IncrementCounts(string key, int index, int count = 1)
    {
        lock (_nodeFailuresLock)
        {
            if (!_nodeFailures.ContainsKey(key))
                _nodeFailures.Add(key, new[] { 0, 0 });

            _nodeFailures[key][index] += count;
        }
    }

    private static void GenerateAggregateCounts()
    {
        // Regex _nodeRegex = new(@"\[\d+]->");
        // lock (_nodeFailuresLock)
        // {
        //     foreach (var failureInfo in _nodeFailures.Where(failureInfo => !_nodeRegex.IsMatch(failureInfo.Key)))
        //     {
        //         // TODO: Actually do something here?
        //     }
        // }
    }
}
