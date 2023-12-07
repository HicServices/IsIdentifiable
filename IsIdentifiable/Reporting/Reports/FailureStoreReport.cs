using CsvHelper;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Destinations;
using IsIdentifiable.Rules;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IsIdentifiable.Reporting.Reports;

/// <summary>
/// <para>
/// <see cref="FailureReport"/> that persists all data in all <see cref="Failure"/> seen into
/// the given <see cref="IReportDestination"/> such that it can be reassembled later (persistence).
/// </para>
/// <para>This report should be used when you want to capture all output in a human readable format
/// but also be able to reload the <see cref="Failure"/> for later processing e.g. by the reviewer</para>
/// </summary>
public class FailureStoreReport : FailureReport
{
    private readonly object _odtLock = new();
    private readonly DataTable _dtAllFailures;

    private readonly int _maxSize;

    private readonly string[] _headerRow = { "Resource", "ResourcePrimaryKey", "ProblemField", "ProblemValue", "PartWords", "PartClassifications", "PartOffsets" };

    private const string Separator = "###";

    /// <summary>
    /// Creates a new report aimed at the given resource (e.g. "MR_ImageTable")
    /// </summary>
    /// <param name="targetName"></param>
    /// <param name="maxSize">Max size of the internal store before writing out to file</param>
    /// <param name="fileSystem"></param>
    public FailureStoreReport(string targetName, int maxSize, IFileSystem fileSystem)
        : base(targetName, fileSystem)
    {
        _dtAllFailures = new DataTable();

        foreach (var s in _headerRow)
            _dtAllFailures.Columns.Add(s);

        if (maxSize < 0)
            throw new ArgumentException("maxSize must be positive");

        _maxSize = maxSize;
    }

    /// <summary>
    /// Adds all destinations described in <paramref name="opts"/> to the list of output
    /// locations for this report.  Also writes the report headers to the destinations.
    /// </summary>
    /// <param name="opts"></param>
    public override void AddDestinations(IsIdentifiableOptions opts)
    {
        base.AddDestinations(opts);
        Destinations.ForEach(d => d.WriteHeader((from dc in _dtAllFailures.Columns.Cast<DataColumn>() select dc.ColumnName).ToArray()));
    }

    /// <summary>
    /// Adds a custom <paramref name="destination"/> as an output adapter for this
    /// report.  Immediately calls <see cref="IReportDestination.WriteHeader(string[])"/>
    /// with the report headers.
    /// </summary>
    /// <param name="destination"></param>
    public void AddDestination(IReportDestination destination)
    {
        Destinations.Add(destination);
        destination.WriteHeader((from dc in _dtAllFailures.Columns.Cast<DataColumn>() select dc.ColumnName).ToArray());
    }

    /// <summary>
    /// Writes the <paramref name="failure"/> to all <see cref="Destinations"/>.  The full
    /// contents of the <see cref="Failure"/> are written such that it can be reloaded
    /// later on from the outputted representation (deserialized)
    /// </summary>
    /// <param name="failure"></param>
    public override void Add(Failure failure)
    {
        lock (_odtLock)
        {
            _dtAllFailures.Rows.Add(
                failure.Resource,
                failure.ResourcePrimaryKey,
                failure.ProblemField,
                failure.ProblemValue,
                string.Join(Separator, failure.Parts.Select(p => p.Word)),
                string.Join(Separator, failure.Parts.Select(p => p.Classification)),
                string.Join(Separator, failure.Parts.Select(p => p.Offset)));

            if (_dtAllFailures.Rows.Count < _maxSize)
                return;

            CloseReportBase();
            _dtAllFailures.Clear();
        }
    }

    /// <summary>
    /// Writes the current batch of failures to all destinations.  This method
    /// is designed to be called multiple times (i.e. it is not a dispose pattern)
    /// </summary>
    protected override void CloseReportBase()
    {
        Destinations.ForEach(d => d.WriteItems(_dtAllFailures));
    }

    /// <summary>
    /// Opens a file generated by this class (when using <see cref="CsvDestination"/>) and parses
    /// the contents of the file to generate all the <see cref="Failure"/> recorded in it.
    /// </summary>
    /// <param name="oldFile">CSV file containing <see cref="Failure"/> instances serialized by this class</param>
    /// <returns></returns>
    public static IEnumerable<Failure> Deserialize(IFileInfo oldFile)
    {
        return Deserialize(oldFile, (s) => { }, CancellationToken.None);
    }

    /// <summary>
    /// Opens a file generated by this class (when using <see cref="CsvDestination"/>) and parses
    /// the contents of the file to generate all the <see cref="Failure"/> recorded in it.
    /// </summary>
    /// <param name="oldFile">CSV file containing <see cref="Failure"/> instances serialized by this class</param>
    /// <param name="loadedRows">Action to call periodically as records are read from the file (for
    /// when the file is very big and you want to show progress etc)</param>
    /// <param name="token">Cancellation token for aborting the file deserialication (and closing the file again)</param>
    /// <param name="partRules"></param>
    /// <param name="runParallel"></param>
    /// <param name="stopAtFirstError"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static IEnumerable<Failure> Deserialize(IFileInfo oldFile, Action<int> loadedRows, CancellationToken token, IEnumerable<PartPatternFilterRule>? partRules = null, bool runParallel = true, bool stopAtFirstError = false)
    {
        partRules ??= new List<PartPatternFilterRule>();

        using var stream = oldFile.OpenRead();
        using var sr = new System.IO.StreamReader(stream);
        using var reader = new CsvReader(sr, System.Globalization.CultureInfo.CurrentCulture);
        if (reader.Read())
            reader.ReadHeader();
        else
            return Enumerable.Empty<Failure>();

        int totalProcessed = 0;
        var localTokenSource = new CancellationTokenSource();
        var failures = new ConcurrentBag<Failure>();

        if (runParallel)
        {
            using var timerTask = Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested && !localTokenSource.Token.IsCancellationRequested)
                {
                    loadedRows(totalProcessed);
                    await Task.Delay(TimeSpan.FromSeconds(0.1), token);
                }
            },
            token
            );

            try
            {
                Parallel.ForEach(
                   reader.GetRecords<FailureStoreReportRecord>(),
                   new ParallelOptions
                   {
                       CancellationToken = token,
                   },
                   (FailureStoreReportRecord row) => Process(row, partRules, failures, ref totalProcessed)
                );
            }
            finally
            {
                localTokenSource.Cancel();
                timerTask.Wait();
            }
        }
        else
        {
            var problems = 0;
            foreach (var row in reader.GetRecords<FailureStoreReportRecord>())
            {
                try
                {
                    Process(row, partRules, failures, ref totalProcessed);
                }
                catch (Exception e)
                {
                    if (stopAtFirstError)
                    {
                        Console.Error.WriteLine($"{row}:");
                        Console.Error.WriteLine(e);
                        throw;
                    }
                    else
                    {
                        Console.Error.WriteLine($"{row}:\n{e.Message}\n");
                        problems++;
                    }
                }
            }

            if (problems > 0)
                Console.Error.WriteLine($"Problem with {problems}/{totalProcessed} records");
        }

        loadedRows(totalProcessed);

        return failures;
    }

    private static void Process(FailureStoreReportRecord row, IEnumerable<PartPatternFilterRule>? partRules, ConcurrentBag<Failure> failures, ref int totalProcessed)
    {
        if (row.ProblemValue == null)
            throw new Exception("ProblemValue was null");

        var words = row.PartWords.Split(Separator);
        var classes = row.PartClassifications.Split(Separator);
        var offsets = row.PartOffsets.Split(Separator);

        var parts = words.Select(
            (word, index) => new FailurePart(
                word,
                Enum.TryParse<FailureClassification>(classes[index], true, out var classification) ? classification : throw new Exception($"Invalid failure classification '{classes[index]}'"),
                int.TryParse(offsets[index], out var offset) ? offset : throw new Exception($"Invalid offset '{row.PartOffsets}'")
            )
        ).ToList();

        if (row.ProblemField != "PixelData")
        {
            // Fixes any offsets that have been mangled by file endings etc.
            foreach (var part in parts)
            {
                try
                {
                    if (row.ProblemValue.Substring(part.Offset, part.Word.Length) == part.Word)
                        continue;
                }
                catch (ArgumentOutOfRangeException) { }

                // Test if the ProblemValue has been HTML escaped
                var encodedPartWord = WebUtility.HtmlEncode(part.Word);
                try
                {
                    if (row.ProblemValue.Substring(part.Offset, encodedPartWord.Length) == encodedPartWord)
                    {
                        part.Word = encodedPartWord;
                        continue;
                    }
                }
                catch (ArgumentOutOfRangeException)
                { }

                // Test if the ProblemValue has hidden unicode symbols
                var withoutInvisible = Regex.Replace(row.ProblemValue, @"\p{C}+", string.Empty);
                try
                {
                    if (withoutInvisible.Substring(part.Offset, part.Word.Length) == part.Word)
                    {
                        part.Word = row.ProblemValue.Substring(part.Offset, part.Word.Length + 1);

                        if (row.ProblemValue.Substring(part.Offset, part.Word.Length) != part.Word)
                            throw new Exception($"Could not fix hidden unicode characters in Failure:\n===\n{row}\n===");

                        continue;
                    }
                }
                catch (ArgumentOutOfRangeException)
                { }

                // Finally, try shifting the offset around to find the word
                try
                {
                    FixupOffsets(row, part);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new Exception($"Could not fixup Offset value in Failure:\n{row}", e);
                }
            }
        }

        /* TEMP - Filter out any FailureParts covered by an PartPatternFilterRule */
        var toRemove = new List<FailurePart>();
        foreach (var partRule in partRules)
        {
            if (!string.IsNullOrWhiteSpace(partRule.IfColumn) && !string.Equals(partRule.IfColumn, row.ProblemField, StringComparison.InvariantCultureIgnoreCase))
                continue;

            foreach (var part in parts.Where(x => partRule.Covers(x, row.ProblemValue)))
            {
                toRemove.Add(part);
                partRule.IncrementUsed();
            }
        }
        parts = parts.Except(toRemove).ToList();
        /* TEMP */

        if (parts.Any())
            failures.Add(new Failure(parts)
            {
                Resource = row.Resource,
                ResourcePrimaryKey = row.ResourcePrimaryKey,
                ProblemField = row.ProblemField,
                ProblemValue = row.ProblemValue,
            });

        Interlocked.Increment(ref totalProcessed);
    }

    private static void FixupOffsets(FailureStoreReportRecord row, FailurePart part)
    {
        // Try looking ahead first, then back
        var origOffset = part.Offset;
        try
        {
            while (row.ProblemValue.Substring(part.Offset, part.Word.Length) != part.Word)
                part.Offset++;
        }
        catch (ArgumentOutOfRangeException)
        {
            part.Offset = origOffset;
            while (row.ProblemValue.Substring(part.Offset, part.Word.Length) != part.Word)
                part.Offset--;
        }
    }

    internal class FailureStoreReportRecord
    {
        public string Resource { get; init; }
        public string ResourcePrimaryKey { get; init; }
        public string ProblemField { get; init; }
        public string ProblemValue { get; init; }
        public string PartWords { get; init; }
        public string PartClassifications { get; init; }
        public string PartOffsets { get; init; }

        public override string ToString() => $"Failure({Resource}|{ResourcePrimaryKey}|{ProblemField}|{ProblemValue}|{PartWords}|{PartClassifications}|{PartOffsets})";
    }
}
