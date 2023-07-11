using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using IsIdentifiable.Tests.TestUtil;
using NUnit.Framework;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests.ReviewerTests;

public class UnattendedTests
{
    private MockFileSystem _fileSystem = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        ImplementationManager.Load<MicrosoftSQLImplementation>();
        ImplementationManager.Load<MySqlImplementation>();
        ImplementationManager.Load<PostgreSqlImplementation>();
        ImplementationManager.Load<OracleImplementation>();
    }

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }

    [Test]
    public void NonExistantFileToProcess_Throws()
    {
        var ex = Assert.Throws<System.IO.FileNotFoundException>(() =>
            new UnattendedReviewer(
                new DatabaseTargetOptions(),
                null!,
                null!,
                _fileSystem.FileInfo.New("troll.csv"),
                null!
            )
        );

        StringAssert.Contains("Could not find Failures file", ex?.Message);
    }

    [Test]
    public void Passes_NoFailures()
    {
        var failuresCsv = _fileSystem.FileInfo.New("myfile.csv");
        _fileSystem.File.WriteAllText(failuresCsv.FullName, "fff");

        var outputFile = _fileSystem.FileInfo.New("out.csv");

        var reviewer = new UnattendedReviewer(
            new DatabaseTargetOptions(),
            new IgnoreRuleGenerator(_fileSystem),
            new RowUpdater(_fileSystem),
            failuresCsv,
            outputFile
        );

        Assert.AreEqual(0, reviewer.Run());

        //just the headers
        StringAssert.AreEqualIgnoringCase("Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets", _fileSystem.File.ReadAllText(outputFile.FullName).TrimEnd());
    }

    [Test]
    public void Passes_FailuresAllUnprocessed()
    {
        var failuresText = TestHelpers.EnvironmentStringJoin(
            "Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",
            "FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28"
        );

        var failuresCsv = _fileSystem.FileInfo.New("myfile.csv");
        _fileSystem.AddFile(failuresCsv, new MockFileData(failuresText));

        var outputFile = _fileSystem.FileInfo.New("out.csv");

        var reviewer = new UnattendedReviewer(
            new DatabaseTargetOptions(),
            new IgnoreRuleGenerator(_fileSystem),
            new RowUpdater(_fileSystem),
            failuresCsv,
            outputFile
        );

        Assert.AreEqual(0, reviewer.Run());

        //all that we put in is unprocessed so should come out the same
        TestHelpers.AreEqualIgnoringCaseAndLineEndings(failuresText, _fileSystem.File.ReadAllText(outputFile.FullName).TrimEnd());

        Assert.AreEqual(1, reviewer.Total);
        Assert.AreEqual(0, reviewer.Ignores);
        Assert.AreEqual(1, reviewer.Unresolved);
        Assert.AreEqual(0, reviewer.Updates);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Passes_FailuresAllIgnored(bool rulesOnly)
    {
        var failuresText = TestHelpers.EnvironmentStringJoin(
            "Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",
            "FunBooks.HappyOzz, 1.2.3, Narrative, We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28"
        );

        var failuresCsv = _fileSystem.FileInfo.New("myfile.csv");
        _fileSystem.AddFile(failuresCsv, new MockFileData(failuresText));

        var outputFile = _fileSystem.FileInfo.New("out.csv");

        var fiAllowlist = IgnoreRuleGenerator.DefaultFileName;

        //add a Allowlist to ignore these
        _fileSystem.File.WriteAllText(fiAllowlist,
            @"
- Action: Ignore
  IfColumn: Narrative
  IfPattern: ^We\ aren't\ in\ Kansas\ anymore\ Toto$");

        var reviewer = new UnattendedReviewer(
            new DatabaseTargetOptions(),
            new IgnoreRuleGenerator(fileSystem: _fileSystem),
            new RowUpdater(fileSystem: _fileSystem),
            failuresCsv,
            outputFile
        );

        Assert.AreEqual(0, reviewer.Run());

        //headers only since Allowlist eats the rest
        var expected = "Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets";
        StringAssert.AreEqualIgnoringCase(expected, _fileSystem.File.ReadAllText(outputFile.FullName).TrimEnd());

        Assert.AreEqual(1, reviewer.Total);
        Assert.AreEqual(1, reviewer.Ignores);
        Assert.AreEqual(0, reviewer.Unresolved);
        Assert.AreEqual(0, reviewer.Updates);
    }

    // TODO(rkm 2023-07-10) Re-implement this after refactor
    //    [TestCase(true)]
    //    [TestCase(false)]
    //    public void Passes_FailuresAllUpdated(bool ruleCoversThis)
    //    {
    //        var failuresText = TestHelpers.EnvironmentStringJoin(
    //            "Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",
    //            "FunBooks.HappyOzz, 1.2.3, Narrative, We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28"
    //        );
    //        var failuresCsv = _fileSystem.FileInfo.New("myfile.csv");
    //        _fileSystem.AddFile(failuresCsv, new MockFileData(failuresText));

    //        var outputFile = _fileSystem.FileInfo.New("out.csv");

    //        var fiReportlist = RowUpdater.DefaultFileName;

    //        //add a Reportlist to UPDATE these
    //        if (ruleCoversThis)
    //        {
    //            _fileSystem.File.WriteAllText(fiReportlist,
    //                @"
    //- Action: Ignore
    //  IfColumn: Narrative
    //  IfPattern: ^We\ aren't\ in\ Kansas\ anymore\ Toto$");
    //        }

    //        var reviewer = new UnattendedReviewer(new DatabaseTargetOptions()
    //        {
    //            FailuresCsv = fi,
    //            UnattendedOutputPath = fiOut,
    //            OnlyRules = true //prevents it going to the database
    //        }, new Target(), new IgnoreRuleGenerator(fileSystem: _fileSystem), new RowUpdater(fileSystem: _fileSystem), _fileSystem);

    //        Assert.AreEqual(0, reviewer.Run());

    //        //it matches the UPDATE rule but since OnlyRules is true it didn't actually update the database! so the record should definitely be in the output

    //        if (!ruleCoversThis)
    //        {
    //            // no rule covers this so the miss should appear in the output file
    //            TestHelpers.AreEqualIgnoringCaseAndLineEndings(@"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets
    //FunBooks.HappyOzz,1.2.3,Narrative,We aren't in Kansas anymore Toto,Kansas###Toto,Location###Location,13###28", _fileSystem.File.ReadAllText(fiOut).TrimEnd());
    //        }
    //        else
    //        {
    //            // a rule covers this so even though we do not update the database there shouldn't be a 'miss' in the output file
    //            TestHelpers.AreEqualIgnoringCaseAndLineEndings(@"Resource,ResourcePrimaryKey,ProblemField,ProblemValue,PartWords,PartClassifications,PartOffsets",
    //                _fileSystem.File.ReadAllText(fiOut).TrimEnd());
    //        }

    //        Assert.AreEqual(1, reviewer.Total);
    //        Assert.AreEqual(0, reviewer.Ignores);
    //        Assert.AreEqual(ruleCoversThis ? 0 : 1, reviewer.Unresolved);
    //        Assert.AreEqual(ruleCoversThis ? 1 : 0, reviewer.Updates);
    //    }
}
