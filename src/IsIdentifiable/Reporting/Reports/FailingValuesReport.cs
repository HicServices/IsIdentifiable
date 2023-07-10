using IsIdentifiable.Failures;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;

namespace IsIdentifiable.Reporting.Reports;

internal class FailingValuesReport : FailureReport
{
    private readonly object _oFailuresLock = new();
    private readonly Dictionary<string, HashSet<string>> _failures = new();

    public FailingValuesReport(string targetName, IFileSystem fileSystem)
        : base(targetName, fileSystem) { }

    public override void Add(Failure failure)
    {
        lock (_oFailuresLock)
        {
            if (!_failures.ContainsKey(failure.ProblemField))
                _failures.Add(failure.ProblemField, new HashSet<string>(StringComparer.CurrentCultureIgnoreCase));

            _failures[failure.ProblemField].Add(failure.ProblemValue);
        }
    }

    protected override void CloseReportBase()
    {
        using var dt = new DataTable();

        dt.Columns.Add("Field");
        dt.Columns.Add("Value");

        lock (_oFailuresLock)
            foreach (var kvp in _failures)
                foreach (var v in kvp.Value)
                    dt.Rows.Add(kvp.Key, v);

        foreach (var d in Destinations)
            d.WriteItems(dt);
    }
}
