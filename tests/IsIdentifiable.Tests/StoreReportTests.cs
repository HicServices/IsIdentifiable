using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;

namespace IsIdentifiable.Tests;

class StoreReportTests
{
    private MockFileSystem _fileSystem = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }

    [Test]
    public void ReconstructionFromCsv()
    {
        var opts = new IsIdentifiableOptions();
        var dir = _fileSystem.DirectoryInfo.New(".");

        // TODO(rkm 2023-07-10) Refactor this
        //opts.DestinationCsvFolder = dir.FullName;
        //opts.TableName = "HappyOzz";

        var report = new FailureStoreReport("HappyOzz", 1000, _fileSystem);
        report.AddDestinations(opts);

        var failure = new Failure(
            new FailurePart[]
            {
                new("Kansas", FailureClassification.Location, 12),
                new("Toto", FailureClassification.Location, 28)
            })
        {
            ProblemValue = "We aren't in Kansas anymore Toto",
            ProblemField = "Narrative",
            ResourcePrimaryKey = "1.2.3",
            Resource = "FunBooks.HappyOzz"
        };

        report.Add(failure);

        report.CloseReport();

        var created = dir.GetFiles("*HappyOzz*.csv").SingleOrDefault();

        Assert.IsNotNull(created);

        var failures2 = FailureStoreReport.Deserialize(created).ToArray();

        //read failure ok
        Assert.AreEqual(1, failures2.Length);
        Assert.AreEqual(failure.ProblemValue, failures2[0].ProblemValue);
        Assert.AreEqual(failure.ProblemField, failures2[0].ProblemField);
        Assert.AreEqual(failure.ResourcePrimaryKey, failures2[0].ResourcePrimaryKey);
        Assert.AreEqual(failure.Resource, failures2[0].Resource);

        //read parts ok
        Assert.AreEqual(2, failures2[0].Parts.Count);

        Assert.AreEqual(failure.Parts[0].Classification, failures2[0].Parts[0].Classification);
        Assert.AreEqual(failure.Parts[0].Offset, failures2[0].Parts[0].Offset);
        Assert.AreEqual(failure.Parts[0].Word, failures2[0].Parts[0].Word);

        Assert.AreEqual(failure.Parts[1].Classification, failures2[0].Parts[1].Classification);
        Assert.AreEqual(failure.Parts[1].Offset, failures2[0].Parts[1].Offset);
        Assert.AreEqual(failure.Parts[1].Word, failures2[0].Parts[1].Word);

    }


    [Test]
    public void Includes()
    {
        var origin = "this word fff is the problem";

        var part = new FailurePart("fff", FailureClassification.Organization, origin.IndexOf("fff"));

        Assert.IsFalse(part.Includes(0));
        Assert.IsFalse(part.Includes(9));
        Assert.IsTrue(part.Includes(10));
        Assert.IsTrue(part.Includes(11));
        Assert.IsTrue(part.Includes(12));
        Assert.IsFalse(part.Includes(13));
    }

    [Test]
    public void IncludesSingleChar()
    {
        var origin = "this word f is the problem";

        var part = new FailurePart("f", FailureClassification.Organization, origin.IndexOf("f"));

        Assert.IsFalse(part.Includes(0));
        Assert.IsFalse(part.Includes(9));
        Assert.IsTrue(part.Includes(10));
        Assert.IsFalse(part.Includes(11));
        Assert.IsFalse(part.Includes(12));
        Assert.IsFalse(part.Includes(13));
    }

    [Test]
    public void HaveSameProblem()
    {
        var f1 = new Failure(new List<FailurePart>())
        {
            ProblemValue = "Happy fun times",
            ProblemField = "Jokes",
            Resource = "MyTable",
            ResourcePrimaryKey = "1.2.3"
        };
        var f2 = new Failure(new List<FailurePart>())
        {
            ProblemValue = "Happy fun times",
            ProblemField = "Jokes",
            Resource = "MyTable",
            ResourcePrimaryKey = "9.9.9" //same problem different record (are considered to have the same problem)
        };
        var f3 = new Failure(new List<FailurePart>())
        {
            ProblemValue = "Happy times", //different because input value is different
            ProblemField = "Jokes",
            Resource = "MyTable",
            ResourcePrimaryKey = "1.2.3"
        };
        var f4 = new Failure(new List<FailurePart>())
        {
            ProblemValue = "Happy fun times",
            ProblemField = "SensitiveJokes", //different because other column
            Resource = "MyTable",
            ResourcePrimaryKey = "1.2.3"
        };

        Assert.IsTrue(f1.HaveSameProblem(f2));
        Assert.IsFalse(f1.HaveSameProblem(f3));
        Assert.IsFalse(f1.HaveSameProblem(f4));
    }

}
