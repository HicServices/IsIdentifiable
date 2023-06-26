using DicomTypeTranslation;
using FellowOakDicom;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NLog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IsIdentifiable.Runners;

/// <summary>
/// Evaluates data in a mongodb collection
/// </summary>
public class MongoRunner : IsIdentifiableAbstractRunner
{
    private const string SEP = "#";

    private readonly ILogger _logger;

    private readonly IsIdentifiableMongoOptions _opts;

    private readonly TreeFailureReport _treeReport;

    private readonly IMongoCollection<BsonDocument> _collection;

    private readonly string _queryString;

    private readonly FindOptions<BsonDocument> _findOptionsBase = new()
    {
        NoCursorTimeout = true
    };

    private readonly ParallelOptions _parallelOptions = new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1
    };

    private Task _runnerTask;
    private readonly CancellationTokenSource _tokenSource = new();
    private bool _stopping;

    /// <summary>
    /// Creates a new instance and prepares to fetch data from the MongoDb instance
    /// specified in <paramref name="opts"/>
    /// </summary>
    /// <param name="opts"></param>
    /// <param name="fileSystem"></param>
    public MongoRunner(IsIdentifiableMongoOptions opts, IFileSystem fileSystem)
        : base(opts, fileSystem)
    {
        _logger = LogManager.GetLogger(GetType().Name);
        _opts = opts;

        LogProgressNoun = "documents";

        if (opts.TreeReport)
        {
            _treeReport = new TreeFailureReport(opts.GetTargetName(FileSystem), FileSystem);
            Reports.Add(_treeReport);
        }

        var mongoClient = new MongoClient(opts.MongoConnectionString);

        var db = TryGetDatabase(mongoClient, _opts.DatabaseName);
        _collection = TryGetCollection(db, _opts.CollectionName);

        if (!string.IsNullOrWhiteSpace(_opts.QueryFile))
            _queryString = FileSystem.File.ReadAllText(_opts.QueryFile);

        // if specified, batch size must be g.t. 1:
        // https://docs.mongodb.com/manual/reference/method/cursor.batchSize/
        if (_opts.MongoDbBatchSize > 1)
            _findOptionsBase.BatchSize = _opts.MongoDbBatchSize;

        if (_opts.UseMaxThreads)
            _parallelOptions.MaxDegreeOfParallelism = -1;
    }

    /// <summary>
    /// Connects to the MongoDb and evaluates all data in the collection/filter specified
    /// </summary>
    /// <returns></returns>
    public override int Run()
    {
        _runnerTask = RunQuery();
        _runnerTask.Wait();

        return 0;
    }

    private async Task RunQuery()
    {
        _logger.Info($"Using MaxDegreeOfParallelism: {_parallelOptions.MaxDegreeOfParallelism}");

        var totalProcessed = 0;
        var failedToRebuildCount = 0;

        _logger.Debug("Performing query");
        var start = DateTime.Now;

        using (var cursor = await MongoQueryParser.GetCursor(_collection, _findOptionsBase, _queryString))
        {
            _logger.Info("Query completed in {0:g}. Starting checks with cursor", (DateTime.Now - start));
            _logger.Info(
                $"Batch size is: {(_findOptionsBase.BatchSize.HasValue ? _findOptionsBase.BatchSize.ToString() : "unspecified")}");

            start = DateTime.Now;

            //Note: Can only check for the cancellation request every time we start to process a new batch
            while (await cursor.MoveNextAsync() && !_tokenSource.IsCancellationRequested)
            {
                _logger.Debug("Received new batch");

                var batch = cursor.Current;
                var batchCount = 0;

                var batchFailures = new List<Failure>();
                var oListLock = new object();
                var oLogLock = new object();

                Parallel.ForEach(batch, _parallelOptions, document =>
                {
                    var documentId = document["_id"].AsObjectId;

                    if (_opts.IsDicomFiles)
                    {
                        ProcessDocumentAsDicom(document, documentId, oLogLock, oListLock, ref failedToRebuildCount, ref batchCount, batchFailures);
                    }
                    else
                    {
                        ProcessDocumentAsUnstructured(document, documentId, oListLock, ref batchCount, batchFailures);
                    }
                });

                batchFailures.ForEach(AddToReports);

                totalProcessed += batchCount;
                _logger.Debug($"Processed {totalProcessed} documents total");

                DoneRows(batchCount);
            }
        }

        var queryTime = DateTime.Now - start;
        _logger.Info($"Processing finished or cancelled, total time elapsed: {queryTime:g}");

        _logger.Info("{0} documents were processed in total", totalProcessed);

        if (failedToRebuildCount > 0)
            _logger.Warn("{0} documents could not be reconstructed into DicomDatasets", failedToRebuildCount);

        _logger.Info("Writing out reports...");
        CloseReports();
    }

    private void ProcessDocumentAsDicom(BsonDocument document, ObjectId documentId, object oLogLock, object oListLock, ref int failedToRebuildCount, ref int batchCount, List<Failure> batchFailures)
    {

        DicomDataset ds;

        try
        {
            ds = DicomTypeTranslaterWriter.BuildDicomDataset(document);
        }
        catch (Exception e)
        {
            // Log any documents we couldn't process due to errors in rebuilding the dataset
            lock (oLogLock)
                _logger.Log(LogLevel.Error, e,
                    "Could not reconstruct dataset from document " + documentId);

            Interlocked.Increment(ref failedToRebuildCount);

            return;
        }

        // Validate the dataset against our rules
        var documentFailures = ProcessDataset(documentId, ds);

        if (documentFailures.Any())
            lock (oListLock)
                batchFailures.AddRange(documentFailures);

        Interlocked.Increment(ref batchCount);
    }

    private void ProcessDocumentAsUnstructured(BsonDocument document, ObjectId documentId, object oListLock, ref int batchCount, List<Failure> batchFailures)
    {
        // Validate the dataset against our rules
        var documentFailures = ProcessDocument(documentId, "", document);

        if (documentFailures.Any())
            lock (oListLock)
                batchFailures.AddRange(documentFailures);

        Interlocked.Increment(ref batchCount);
    }

    private IList<Failure> ProcessDocument(ObjectId documentId, string tagTree, BsonDocument document)
    {
        var failures = new List<Failure>();

        foreach (var element in document)
        {
            failures.AddRange(ProcessBsonValue(documentId, tagTree, element.Name, element.Value, false));
        }
        return failures;
    }

    private IList<Failure> ProcessBsonValue(ObjectId documentId, string tagTree, string name, BsonValue value, bool isArrayElement)
    {
        var failures = new List<Failure>();

        if (!isArrayElement)
        {
            tagTree += name;
        }

        switch (value.BsonType)
        {
            // sub document
            case BsonType.Document:

                failures.AddRange(
                    ProcessDocument(
                        documentId,
                        $"{tagTree}->",
                        (BsonDocument)value)
                    );
                break;

            // array of values
            case BsonType.Array:

                var i = 0;
                // values could be mixed type
                foreach (var entry in (BsonArray)value)
                {
                    // process each array element
                    failures.AddRange(
                        ProcessBsonValue(documentId, $"{tagTree}[{i}]", name, entry, true)
                        );
                    i++;
                }
                break;
            default:

                failures.AddRange(
                    ValidateBsonValue(documentId, tagTree, name, value)
                    );
                break;
        }

        return failures;
    }

    /// <summary>
    /// Validates a basic (not Document not Array) value
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private IEnumerable<Failure> ValidateBsonValue(ObjectId documentId, string fullTagPath, string name, BsonValue value)
    {
        var valueAsString = value.ToString();
        var parts = Validate(name, valueAsString).ToList();

        if (parts.Any())
            yield return FailureFrom(documentId, fullTagPath, valueAsString, parts);
    }

    private static Failure FailureFrom(ObjectId documentId, string fullTagPath, string value, List<FailurePart> parts)
        => new(parts)
        {
            // No need to set this since the report will be named MongoDB-<database>.<collection>
            Resource = "",

            // Guaranteed to be unique across a collection
            ResourcePrimaryKey = documentId.ToString(),

            ProblemField = fullTagPath,
            ProblemValue = value,
        };

    /// <summary>
    /// Closes any connections or ongoing queries to MongoDb
    /// and shuts down the class
    /// </summary>
    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        base.Dispose();

        if (_stopping)
            return;

        _stopping = true;

        _logger.Info("Cancelling the running query");
        _tokenSource.Cancel();
    }



    private IList<Failure> ProcessDataset(ObjectId documentId, DicomDataset ds, string tagTree = "")
    {
        var nodeCounts = new Dictionary<string, int>();
        var failures = new List<Failure>();

        ds.TryGetString(DicomTag.Modality, out var modality);
        var hasImageType = ds.TryGetValues(DicomTag.ImageType, out string[] imageTypeArr);

        var imageTypeStr = "";

        if (hasImageType)
            imageTypeStr = string.Join(@"\\", imageTypeArr.Take(2));

        // Prefix the Modality and ImageType tags to allow grouping. This is a temporary solution until the reporting API supports grouping.
        var groupPrefix = modality + SEP + imageTypeStr + SEP;

        foreach (var item in ds)
        {
            var kw = item.Tag.DictionaryEntry.Keyword;

            if (item is DicomSequence asSequence)
            {
                for (var i = 0; i < asSequence.Count(); ++i)
                {
                    var subDataset = asSequence.ElementAt(i);
                    var newTagTree = $"{tagTree}{kw}[{i}]->";
                    failures.AddRange(ProcessDataset(documentId, subDataset, newTagTree));
                }

                continue;
            }

            var element = ds.GetDicomItem<DicomElement>(item.Tag);
            var fullTagPath = groupPrefix + tagTree + kw;

            //TODO OverlayRows...
            if (!nodeCounts.TryAdd(fullTagPath, 1))
                nodeCounts[fullTagPath]++;

            if (element.Count == 0)
                continue;

            // If it is not a (multi-)string element, continue
            if (!element.ValueRepresentation.IsString)
                continue;

            // For each string in the element
            //TODO This is slow and should be refactored
            failures.AddRange(ds.GetValues<string>(element.Tag)
                .Select(s => new { s, parts = Validate(kw, s).ToList() })
                .Where(t => t.parts.Any())
                .Select(t => FailureFrom(documentId, fullTagPath, t.s, t.parts)));
        }

        AddNodeCounts(nodeCounts);

        return failures;
    }

    private void AddNodeCounts(IDictionary<string, int> nodeCounts)
    {
        _treeReport?.AddNodeCounts(nodeCounts);
    }

    private static IMongoDatabase TryGetDatabase(MongoClient client, string dbName)
    {
        if (!client.ListDatabaseNames().ToList().Contains(dbName))
            throw new MongoException($"Database '{dbName}' does not exist on the server");

        return client.GetDatabase(dbName);
    }

    private static IMongoCollection<BsonDocument> TryGetCollection(IMongoDatabase database, string collectionName)
    {
        var listOptions = new ListCollectionNamesOptions
        {
            Filter = new BsonDocument("name", collectionName)
        };

        if (!database.ListCollectionNames(listOptions).Any())
            throw new MongoException(
                $"Collection '{collectionName}' does not exist in database {database.DatabaseNamespace}");

        return database.GetCollection<BsonDocument>(collectionName);
    }

    private class MongoQueryParser
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public static async Task<IAsyncCursor<BsonDocument>> GetCursor(IMongoCollection<BsonDocument> coll, FindOptions<BsonDocument> findOptions, string jsonQuery)
        {
            if (string.IsNullOrWhiteSpace(jsonQuery))
            {
                _logger.Info("No query specified, fetching all records in collection");
                return await coll.FindAsync(FilterDefinition<BsonDocument>.Empty, findOptions);
            }

            BsonDocument docQuery;

            try
            {
                docQuery = BsonSerializer.Deserialize<BsonDocument>(jsonQuery);
                _logger.Info($"Deserialized BsonDocument from string: {docQuery}");
            }
            catch (FormatException e)
            {
                throw new ApplicationException("Could not deserialize the string into a json object", e);
            }

            // Required

            if (!TryParseDocumentProperty(docQuery, "find", out var find))
                throw new ApplicationException("Parsed document did not contain a \"find\" node");

            // Optional

            if (TryParseDocumentProperty(docQuery, "sort", out var sort))
                findOptions.Sort = sort;

            if (TryParseIntProperty(docQuery, "limit", out var limit))
                findOptions.Limit = limit;

            if (TryParseIntProperty(docQuery, "skip", out var skip))
                findOptions.Skip = skip;


            return await coll.FindAsync(find, findOptions);
        }

        private static bool TryParseDocumentProperty(BsonDocument docQuery, string propertyName, out BsonDocument propertyDocument)
        {
            if (docQuery.TryGetValue(propertyName, out var value))
                try
                {
                    propertyDocument = value.AsBsonDocument;
                    _logger.Info($"Parsed document {propertyDocument} for property {propertyName}");

                    return true;
                }
                catch (InvalidCastException e)
                {
                    throw new ApplicationException(
                        $"Could not cast value {value} to a document for property {propertyName}", e);
                }

            _logger.Info($"No document found for property {propertyName}");
            propertyDocument = null;

            return false;
        }

        private static bool TryParseIntProperty(BsonDocument docQuery, string propertyName, out int propertyValue)
        {
            if (docQuery.TryGetValue(propertyName, out var value))
            {
                try
                {
                    propertyValue = value.AsInt32;
                    _logger.Info($"Parsed value {propertyValue} for property {propertyName}");
                }
                catch (InvalidCastException e)
                {
                    throw new ApplicationException(
                        $"Could not cast value {value} to an int for property {propertyName}", e);
                }

                if (propertyValue < 0)
                    throw new ApplicationException($"Property value for {propertyName} must be greater than 0");

                return true;
            }

            _logger.Info($"No value found for property {propertyName}");
            propertyValue = -1;

            return false;
        }
    }
}
