using FAnsi;
using IsIdentifiable.Failures;
using IsIdentifiable.Redacting;
using IsIdentifiable.Redacting.UpdateStrategies;
using IsIdentifiable.Tests.TestUtil;
using Moq;
using NUnit.Framework;
using System.Data;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests.ReviewerTests;

class TestUpdater : DatabaseTests
{
    private MockFileSystem _fileSystem;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }

    [TestCase(DatabaseType.MySql)]
    [TestCase(DatabaseType.MicrosoftSQLServer)]
    [TestCase(DatabaseType.Oracle)]
    [TestCase(DatabaseType.PostgreSql)]
    public void Test(DatabaseType dbType)
    {
        var db = GetTestDatabase(dbType, true);
        var dbname = db.GetRuntimeName();

        var failure = new Failure(
            new FailurePart[]
            {
                new("Kansas", FailureClassification.Location, 13),
                new("Toto", FailureClassification.Location, 28)
            })
        {
            ProblemValue = "We aren't in Kansas anymore Toto",
            ProblemField = "Narrative",
            ResourcePrimaryKey = "1.2.3.4",
            Resource = $"{dbname}.HappyOzz"
        };

        using var dt = new DataTable();
        dt.Columns.Add("MyPk");
        dt.Columns.Add("Narrative");

        dt.PrimaryKey = new[] { dt.Columns["MyPk"] };

        dt.Rows.Add("1.2.3.4", "We aren't in Kansas anymore Toto");

        var tbl = db.CreateTable("HappyOzz", dt);

        //redacted string will be longer! 
        var col = tbl.DiscoverColumn("Narrative");
        col.DataType.Resize(1000);

        var newRules = _fileSystem.FileInfo.New("Reportlist.yaml");

        var updater = new RowUpdater(_fileSystem, newRules)
        {
            UpdateStrategy = new ProblemValuesUpdateStrategy()
        };

        //it should be novel i.e. require user decision
        Assert.IsTrue(updater.OnLoad(db.Server, failure, out _));

        updater.Update(db.Server, failure, null);

        var result = tbl.GetDataTable();
        Assert.AreEqual("We aren't in SMI_REDACTED anymore SMI_REDACTED", result.Rows[0]["Narrative"]);

        TestHelpers.Contains(
            @"- Action: Report
  IfColumn: Narrative
  As: Location
  IfPattern: ^We\ aren't\ in\ Kansas\ anymore\ Toto$
", _fileSystem.File.ReadAllText(newRules.FullName)); //btw slash space is a 'literal space' so legit

        //it should be updated automatically and not require user decision
        Assert.IsFalse(updater.OnLoad(db.Server, failure, out _));

    }


    [TestCase(DatabaseType.MySql, true)]
    [TestCase(DatabaseType.MySql, false)]
    [TestCase(DatabaseType.MicrosoftSQLServer, true)]
    [TestCase(DatabaseType.MicrosoftSQLServer, false)]
    [TestCase(DatabaseType.Oracle, true)]
    [TestCase(DatabaseType.Oracle, false)]
    [TestCase(DatabaseType.PostgreSql, true)]
    [TestCase(DatabaseType.PostgreSql, false)]
    public void Test_RegexUpdateStrategy(DatabaseType dbType, bool provideCaptureGroup)
    {
        var db = GetTestDatabase(dbType, true);
        var dbname = db.GetRuntimeName();

        //the Failure was about Kansas and Toto
        var failure = new Failure(
            new FailurePart[]
            {
                new("Kansas", FailureClassification.Location, 13),
                new("Toto", FailureClassification.Location, 28)
            })
        {
            ProblemValue = "We aren't in Kansas anymore Toto",
            ProblemField = "Narrative",
            ResourcePrimaryKey = "1.2.3.4",
            Resource = $"{dbname}.HappyOzz"
        };

        using var dt = new DataTable();
        dt.Columns.Add("MyPk");
        dt.Columns.Add("Narrative");

        dt.PrimaryKey = new[] { dt.Columns["MyPk"] };

        dt.Rows.Add("1.2.3.4", "We aren't in Kansas anymore Toto");

        var tbl = db.CreateTable("HappyOzz", dt);

        //redacted string will be longer! 
        var col = tbl.DiscoverColumn("Narrative");
        col.DataType.Resize(1000);

        var newRules = _fileSystem.FileInfo.New("Reportlist.yaml");

        var updater = new RowUpdater(_fileSystem, newRules);

        //But the user told us that only the Toto bit is a problem
        var rule = provideCaptureGroup ? "(Toto)$" : "Toto$";
        updater.RulesFactory = Mock.Of<IRulePatternFactory>(m => m.GetPattern(It.IsAny<object>(), It.IsAny<Failure>()) == rule);

        //this is the thing we are actually testing, where we update based on the usersRule not the failing parts
        updater.UpdateStrategy = new RegexUpdateStrategy();

        //it should be novel i.e. require user decision
        Assert.IsTrue(updater.OnLoad(db.Server, failure, out _));

        updater.Update(db.Server, failure, null);//<- null here will trigger the rule pattern factory to prompt 'user' for pattern which is "(Toto)$"

        var result = tbl.GetDataTable();

        if (provideCaptureGroup)
            Assert.AreEqual("We aren't in Kansas anymore SMI_REDACTED", result.Rows[0]["Narrative"], "Expected update to only affect the capture group ToTo");
        else
            Assert.AreEqual("We aren't in SMI_REDACTED anymore SMI_REDACTED", result.Rows[0]["Narrative"], "Because regex had no capture group we expected the update strategy to fallback on Failure Part matching");

        //it should be updated automatically and not require user decision
        Assert.IsFalse(updater.OnLoad(db.Server, failure, out _));

    }
}
