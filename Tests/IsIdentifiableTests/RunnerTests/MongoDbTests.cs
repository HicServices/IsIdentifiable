using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Rules;
using IsIdentifiable.Runners;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Tests.RunnerTests
{
    internal class MongoDbTests
    {
        private MockFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
        }

        public static MongoClientSettings GetMongoClientSettings()
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            using var sr = new System.IO.StreamReader(System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "Mongo.yaml"));
            return deserializer.Deserialize<A>(sr);
        }

        class A : MongoClientSettings
        {
            private string _host;
            private int _port;

            public string Host
            {
                get => _host;
                set
                {
                    _host = value;
                    Server = new MongoServerAddress(_host, _port);
                }
            }

            public int Port
            {
                get => _port;
                set
                {
                    _port = value;
                    Server = new MongoServerAddress(_host, _port);
                }
            }

            public A()
            {

                DirectConnection = true;
                ConnectTimeout = new TimeSpan(0, 0, 0, 5);
                SocketTimeout = new TimeSpan(0, 0, 0, 5);
                HeartbeatTimeout = new TimeSpan(0, 0, 0, 5);
                ServerSelectionTimeout = new TimeSpan(0, 0, 0, 5);
                WaitQueueTimeout = new TimeSpan(0, 0, 05);
            }
            public override string ToString()
            {
                return Host + ":" + Port;
            }
        }
        private MongoClient GetMongoClient()
        {

            var address = GetMongoClientSettings();

            TestContext.Out.WriteLine("Checking the following configuration:" + Environment.NewLine + address);

            var client = new MongoClient(address);

            try
            {
                using var _ = client.ListDatabases();
            }
            catch (Exception e)
            {
                var msg =
                    e is MongoNotPrimaryException
                    ? "Connected to non-primary MongoDB server. Check replication is enabled"
                    : $"Could not connect to MongoDB at {address}";

                msg += $": {e}";

                Assert.Fail(msg);
            }

            return client;
        }
        [Test]
        public void TestConnectingToMongoDb()
        {
            var client = GetMongoClient();
            var cursor = client.ListDatabases();
            cursor.Dispose();
            
        }

        public const string ColorJson =
            @"{ Colors: [
	{
		color: ""red"",
		value: ""#f00""
	},
	{
		color: ""green"",
		value: ""#0f0""
	},
	{
		color: ""blue"",
		value: ""#00f""
	},
	{
		color: ""cyan"",
		value: ""#0ff""
	},
	{
		color: ""magenta"",
		value: ""#f0f""
	},
	{
		color: ""yellow"",
		value: ""#ff0""
	},
	{
		color: ""black"",
		value: ""#000""
	}
]}";


        static object[] TestDocuments =
        {
            new object[] { "{Name: \"hello\"}","Name", "[a-z]+","Name","hello"},
            new object[] { "{Name: [\"Clem\", \"Fandango\"]}","Name", "Fand.*", "Name[1]","Fandango"},
            new object[] { ColorJson,null, "magenta","Colors[4]->color","magenta"},
        };


        /// <summary>
        /// Tests picking up identifiable data in root level or sublevel tags of MongoDb documents
        /// </summary>
        /// <param name="json">The full JSON to insert into the MongoDb database</param>
        /// <param name="column">The tag name you want the <paramref name="regex"/> applied to to detect the problem data or null for 'match any tag'</param>
        /// <param name="regex">The pattern that should be applied to match identifiable data.  In a real world scenario this wouldn't be needed because
        /// this function would come from NLP or existing rules base</param>
        /// <param name="expectedFullPath">The fully expressed path you expect to see indicated in reports when picking up this pattern (MongoDb documents are tree data structures)</param>
        /// <param name="expectedFailingValue">The leaf value that your problem data should be found in (i.e. tag name)</param>
        [TestCaseSource(nameof(TestDocuments))]
        public void TestAnalyseIdentifiableDataInMongoDb(string json, string column, string regex, string expectedFullPath,string expectedFailingValue)
        {
            const string collectionName = "SomeData";
            const string databaseName = "IsIdentifiableTests";

            var client = GetMongoClient();
            var db = client.GetDatabase(databaseName);

            // remove any old data from previous test runs
            db.DropCollection(collectionName);
            db.CreateCollection(collectionName);

            var doc = BsonDocument.Parse(json);

            // Insert document into database
            var collection =
                db.GetCollection<BsonDocument>(collectionName);

            var res = collection.BulkWrite(new[] {
                doc
            }.Select(d => new InsertOneModel<BsonDocument>(d)));

            Assert.IsTrue(res.IsAcknowledged);

            var read = collection.WithReadConcern(ReadConcern.Available);
            Assert.AreEqual(1, read.CountDocuments((d)=>true), "There should be exactly 1 document in the collection");

            var settings = GetMongoClientSettings();

            var runner = new MongoRunner(new IsIdentifiableMongoOptions
            {
                DatabaseName = databaseName,
                CollectionName = collectionName,
                MongoConnectionString = $"mongodb://{settings.Server}",
                StoreReport = true
            }, _fileSystem) ;

            runner.CustomRules.Add(new RegexRule
            {
                Action = RuleAction.Report,
                As = Failures.FailureClassification.Person,
                IfColumn = column,
                IfPattern = regex
            });

            var toMem = new ToMemoryFailureReport();
            runner.Reports.Add(toMem);

            Assert.AreEqual(0,runner.Run(),"MongoDb runner returned a non zero exit code. Indicating failure");
            Assert.AreEqual(1, runner.CountOfFailureParts, "IsIdentifiable did not find exactly 1 failing value, this test only caters for single matches");
            

            var f = toMem.Failures.Single();
            Assert.AreEqual(expectedFullPath, f.ProblemField);
            Assert.AreEqual(expectedFailingValue, f.ProblemValue);
        }
    }
}
