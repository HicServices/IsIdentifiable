using IsIdentifiable.Failures;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Threading;

namespace IsIdentifiable.Reporting.Reports;

internal class ColumnFailureReport : FailureReport
{
    private int _rowsProcessed;

    private readonly object _oFailureCountLock = new();
    private readonly Dictionary<string, int> _failureCounts = new();


    public ColumnFailureReport(string targetName, IFileSystem fileSystem)
        : base(targetName, fileSystem) { }

    public override void DoneRows(int numberDone)
    {
        Interlocked.Add(ref _rowsProcessed, numberDone);
    }

    public override void Add(Failure failure)
    {
        lock (_oFailureCountLock)
        {
            if (!_failureCounts.ContainsKey(failure.ProblemField))
                _failureCounts.Add(failure.ProblemField, 0);

            _failureCounts[failure.ProblemField]++;
        }
    }

    protected override void CloseReportBase()
    {
        if (_rowsProcessed == 0)
            throw new Exception("No rows were processed");

        using var dt = new DataTable();

        lock (_oFailureCountLock)
        {
            foreach (var col in _failureCounts.Keys)
                dt.Columns.Add(col);

            var r = dt.Rows.Add();

            foreach (var kvp in _failureCounts)
                r[kvp.Key] = ((double)kvp.Value) / _rowsProcessed;
        }

        foreach (var d in Destinations)
            d.WriteItems(dt);
    }
}
