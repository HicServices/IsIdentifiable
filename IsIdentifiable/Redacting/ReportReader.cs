﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using IsIdentifiable.Reporting;
using IsIdentifiable.Reporting.Reports;

namespace IsIdentifiable.Redacting;

/// <summary>
/// Reader for CSV failure reports (generated by <see cref="FailureStoreReport"/>).
/// Records progress and holds the <see cref="Failure"/> instances 
/// </summary>
public class ReportReader
{
    private int _current = -1;

    /// <summary>
    /// All failures in the store report read
    /// </summary>
    public Failure[] Failures { get; set; }

    /// <summary>
    /// The current progress made through <see cref="Failures"/> during
    /// redacting/reviewing
    /// </summary>
    public int CurrentIndex => _current;

    /// <summary>
    /// The <see cref="CurrentIndex"/> of <see cref="Failures"/> or null
    /// </summary>
    public Failure Current => _current < Failures.Length ? Failures[_current] : null;
        
    /// <summary>
    /// True if <see cref="CurrentIndex"/> is after the end of the <see cref="Failures"/>
    /// </summary>
    public bool Exhausted => !(_current < Failures.Length);

    /// <summary>
    /// Reads the <paramref name="csvFile"/> and populates <see cref="Failures"/>
    /// </summary>
    /// <param name="csvFile">A CSV file created by a <see cref="FailureStoreReport"/></param>
    public ReportReader(FileInfo csvFile)
    {
        var report = new FailureStoreReport("", 0);
        Failures = report.Deserialize(csvFile).ToArray();
    }

    /// <summary>
    /// Overload that reports progress through the <paramref name="csvFile"/> and cancellation
    /// </summary>
    /// <param name="csvFile"></param>
    /// <param name="loadedRows"></param>
    /// <param name="token"></param>
    public ReportReader(FileInfo csvFile, Action<int> loadedRows, CancellationToken token)
    {
        var report = new FailureStoreReport("", 0);
        Failures = report.Deserialize(csvFile, loadedRows, token).ToArray();
    }

    /// <summary>
    /// Advances <see cref="CurrentIndex"/> along 1
    /// </summary>
    /// <returns>Returns true if a new <see cref="Current"/> is now available or false if it is at the end</returns>
    public bool Next()
    {
        _current++;
        if (_current < Failures.Length)
            return true;

        _current = Failures.Length;
        return false;
    }

    /// <summary>
    /// Updates <see cref="CurrentIndex"/> to the given value bounded
    /// by the total number of <see cref="Failures"/>
    /// </summary>
    /// <param name="index"></param>
    public void GoTo(int index)
    {
        _current = Math.Min(Math.Max(0, index), Failures.Length);
    }

    /// <summary>
    /// Provides a human readable count of how far through the <see cref="Failures"/>
    /// the <see cref="CurrentIndex"/> is.
    /// </summary>
    /// <returns></returns>
    public string DescribeProgress()
    {
        return $"{_current}/{Failures.Length}";
    }
}