using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Scanners;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;

namespace IsIdentifiable.Tests.Scanners;

public class ResourceScannerBaseTests
{
    private MockFileSystem _fileSystem = null!;
    private TestScannerBaseOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
        _options = new TestScannerBaseOptions();
    }

    private class TestScanner : ResourceScannerBase
    {
        private readonly string _fieldToTest;

        public TestScanner(
            TestScannerBaseOptions options,
            MockFileSystem fileSystem,
            string fieldToTest = "field",
            params IFailureReport[] reports
        )
            : base(options, fileSystem, reports)
        {
            _fieldToTest = fieldToTest;
        }

        public IList<FailurePart> Scan(string value)
        {
            var failureParts = Validate(_fieldToTest, value).OrderBy(v => v.Offset).ToList();
            CloseReports();
            return failureParts;
        }

        protected override string LogProgressNoun() => "things";

        protected override void DisposeImpl() { }
    }

    private class TestScannerBaseOptions : ResourceScannerBaseOptions
    {

    }

    [Test]
    public void ChiInString()
    {
        var scanner = new TestScanner(_options, _fileSystem);
        var failurePart = scanner.Scan("hey there,0101010101 excited to see you").Single();

        Assert.AreEqual("0101010101", failurePart.Word);
        Assert.AreEqual(10, failurePart.Offset);
    }

    [Test]
    public void ChiBadDate()
    {
        var scanner = new TestScanner(_options, _fileSystem);
        var failureParts = scanner.Scan("2902810123 would be a CHI if 1981 had been a leap year");
        Assert.IsEmpty(failureParts);
    }

    [Test]
    public void Caching()
    {
        var scanner = new TestScanner(_options, _fileSystem);

        var value = "hey there,0101010101 excited to see you";
        scanner.Scan(value);
        Assert.AreEqual(0, scanner.ValidationCacheHits);
        Assert.AreEqual(1, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(1, scanner.ValidationCacheHits);
        Assert.AreEqual(1, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(2, scanner.ValidationCacheHits);
        Assert.AreEqual(1, scanner.ValidationCacheMisses);

        value = "ffffff";
        scanner.Scan(value);
        Assert.AreEqual(2, scanner.ValidationCacheHits);
        Assert.AreEqual(2, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(3, scanner.ValidationCacheHits);
        Assert.AreEqual(2, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(4, scanner.ValidationCacheHits);
        Assert.AreEqual(2, scanner.ValidationCacheMisses);

        value = "OtherField";
        scanner.Scan(value);
        Assert.AreEqual(4, scanner.ValidationCacheHits);
        Assert.AreEqual(3, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(5, scanner.ValidationCacheHits);
        Assert.AreEqual(3, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(6, scanner.ValidationCacheHits);
        Assert.AreEqual(3, scanner.ValidationCacheMisses);
    }

    [Test]
    public void NoCaching()
    {
        var scanner = new TestScanner(
            new TestScannerBaseOptions
            {
                ValidationCacheLimit = 0,
            },
            _fileSystem
        );

        var value = "hey there,0101010101 excited to see you";
        scanner.Scan(value);
        Assert.AreEqual(0, scanner.ValidationCacheHits);
        Assert.AreEqual(1, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(0, scanner.ValidationCacheHits);
        Assert.AreEqual(2, scanner.ValidationCacheMisses);
        scanner.Scan(value);
        Assert.AreEqual(0, scanner.ValidationCacheHits);
        Assert.AreEqual(3, scanner.ValidationCacheMisses);
    }

    [TestCase("DD3 7LB")]
    [TestCase("dd3 7lb")]
    [TestCase("dd37lb")]
    public void Postcodes(string code)
    {
        var scanner = new TestScanner(_options, _fileSystem);
        var failurePart = scanner.Scan($"Patient lives at {code}").Single();

        Assert.AreEqual(code, failurePart.Word);
        Assert.AreEqual(17, failurePart.Offset);
        Assert.AreEqual(FailureClassification.Postcode, failurePart.Classification);
    }

    // TODO(rkm 2023-07-09) Fixup after AllowList refactor
    //    [TestCase("DD3 7LB")]
    //    [TestCase("dd3 7lb")]
    //    [TestCase("dd37lb")]
    //    public void Postcodes_AllowlistDD3(string code)
    //    {
    //        var scanner = new TestScanner(_options, _fileSystem);

    //        scanner.LoadRules(
    //            @"
    //BasicRules:
    //  - Action: Ignore
    //    IfPattern: DD3");

    //        scanner.Scan($"Patient lives at {code}");

    //        Assert.IsEmpty(scanner.ResultsOfValidate);
    //    }

    [TestCase("DD3 7LB")]
    [TestCase("dd3 7lb")]
    [TestCase("dd37lb")]
    public void Postcodes_IgnorePostcodesFlagSet(string code)
    {
        var scanner = new TestScanner(
            new TestScannerBaseOptions
            {
                IgnorePostcodes = true,
            },
            _fileSystem
        );

        var failureParts = scanner.Scan($"Patient lives at {code}");

        Assert.IsEmpty(failureParts);
    }

    [TestCase("^DD28DD^", "DD28DD")]
    [TestCase("dd3^7lb", "dd3 7lb")]
    public void Postcodes_EmbeddedInText(string value, string expectedMatch)
    {
        var scanner = new TestScanner(_options, _fileSystem);

        var failurePart = scanner.Scan(value).Single();

        Assert.AreEqual(expectedMatch, failurePart.Word);
        Assert.AreEqual(FailureClassification.Postcode, failurePart.Classification);
        Assert.AreEqual(1, scanner.FailurePartCount);
    }

    [TestCase("dd3000")]
    [TestCase("dd3 000")]
    [TestCase("1444DD2011FD1118E63006097D2DF4834C9D2777977D811907000065B840D9CA50000000837000000FF0100A601000000003800A50900000700008001000000AC020000008000000D0000805363684772696400A8480000E6FBFFFF436174616C6F6775654974656D07000000003400A50900000700008002000000A402000000800000090000805363684772696400A84800001E2D0000436174616C6F67756500000000008000A50900000700008003000000520000000180000058000080436F6E74726F6C00A747000")]
    public void NotAPostcode(string code)
    {
        var scanner = new TestScanner(_options, _fileSystem);

        var failureParts = scanner.Scan($"Patient lives at {code}");

        Assert.IsEmpty(failureParts);
    }

    [TestCase("Friday, 29 May 2015", "29 May", "May 2015", null)]
    [TestCase("Friday, 29 May 2015 05:50", "29 May", "May 2015", null)]
    [TestCase("Friday, 29 May 2015 05:50 AM", "29 May", "May 2015", null)]
    [TestCase("Friday, 29th May 2015 5:50", "29th May", "May 2015", null)]
    [TestCase("Friday, May 29th 2015 5:50 AM", "May 29th", null, null)]
    [TestCase("Friday, 29-May-2015 05:50:06", "29-May", "May-2015", null)]
    [TestCase("05/29/2015 05:50", "05/29/2015", null, null)]
    [TestCase("05-29-2015 05:50 AM", "05-29-2015", null, null)]
    [TestCase("2015-05-29 5:50", "2015-05-29", null, null)]
    [TestCase("05/29/2015 5:50 AM", "05/29/2015", null, null)]
    [TestCase("05/29/2015 05:50:06", "05/29/2015", null, null)]
    [TestCase("May-29", "May-29", null, null)]
    [TestCase("Jul-29th", "Jul-29th", null, null)]
    [TestCase("July-1st", "July-1st", null, null)]
    [TestCase("2015-05-16T05:50:06.7199222-04:00", "2015-05-16T", null, null)]
    [TestCase("2015-05-16T05:50:06", "2015-05-16T", null, null)]
    [TestCase("Fri, 16 May 2015 05:50:06 GMT", "16 May", "May 2015", null)]
    [TestCase("2015 May", "2015 May", null, null)]
    public void Dates(string date, string expectedMatch1, string expectedMatch2, string expectedMatch3)
    {
        var scanner = new TestScanner(_options, _fileSystem);
        var failureParts = scanner.Scan($"Patient next appointment is {date}");

        Assert.AreEqual(expectedMatch1, failureParts[0].Word);
        Assert.AreEqual(FailureClassification.Date, failureParts[0].Classification);

        if (expectedMatch2 != null)
        {
            Assert.AreEqual(expectedMatch2, failureParts[1].Word);
            Assert.AreEqual(FailureClassification.Date, failureParts[1].Classification);
        }
        if (expectedMatch3 != null)
        {
            Assert.AreEqual(expectedMatch3, failureParts[2].Word);
            Assert.AreEqual(FailureClassification.Date, failureParts[2].Classification);
        }
    }

    [TestCase("We are going to the pub on Friday at about 3'o clock")]
    [TestCase("We may go there in August some time")]
    [TestCase("I will be 30 in September")]
    [TestCase("Prescribed volume is is 32.0 ml")]
    [TestCase("2001.1.2")]
    [TestCase("AB13:10")]
    public void NotADate(string value)
    {
        var scanner = new TestScanner(_options, _fileSystem);

        var failureParts = scanner.Scan(value);

        Assert.IsEmpty(failureParts);
    }

    [Test]
    public void ChiAndNameInString()
    {
        var scanner = new TestScanner(_options, _fileSystem);

        var failureParts = scanner.Scan("David Smith should be referred to with chi 0101010101");

        Assert.AreEqual(1, failureParts.Count);
        Assert.AreEqual("0101010101", failureParts[0].Word);
        Assert.AreEqual(43, failureParts[0].Offset);
    }

    // TODO(rkm 2023-07-09) Fixup after AllowList refactor
    //[TestCase(true)]
    //[TestCase(false)]
    //public void CaseSensitivity(bool caseSensitive)
    //{
    //    var scanner = new TestScanner(_options, _fileSystem);

    //    scanner.CustomRules.Add(new RegexRule()
    //    {
    //        IfPattern = "ff",
    //        Action = RuleAction.Ignore,
    //        CaseSensitive = caseSensitive
    //    });

    //    scanner.CustomRules.Add(new RegexRule() { IfPattern = "\\w+", Action = RuleAction.Report, As = FailureClassification.Person });

    //    var failureParts = scanner.Scan("FF");

    //    if (caseSensitive)
    //        Assert.AreEqual(1, failureParts.Count);
    //    else
    //        Assert.IsEmpty(failureParts);
    //}

    // TODO(rkm 2023-07-09) Fixup after AllowList refactor
    ///// <summary>
    ///// This tests that the rule order is irrelevant.  Ignore rules should always be applied before report rules
    ///// </summary>
    ///// <param name="ignoreFirst"></param>
    //[TestCase(true)]
    //[TestCase(false)]
    //public void RuleOrdering_BlackBox(bool ignoreFirst)
    //{
    //    var scanner = new TestScanner("FF", _fileSystem);

    //    if (ignoreFirst)
    //    {
    //        //ignore the report
    //        scanner.CustomRules.Add(new RegexRule { IfPattern = "FF", Action = RuleAction.Ignore });
    //        scanner.CustomRules.Add(new RegexRule() { IfPattern = "\\w+", Action = RuleAction.Report, As = FailureClassification.Person });
    //    }
    //    else
    //    {
    //        //report then ignore
    //        scanner.CustomRules.Add(new RegexRule() { IfPattern = "\\w+", Action = RuleAction.Report, As = FailureClassification.Person });
    //        scanner.CustomRules.Add(new RegexRule { IfPattern = "FF", Action = RuleAction.Ignore });
    //    }

    //    scanner.SortRules();

    //    scanner.Scan();

    //    Assert.IsEmpty(scanner.ResultsOfValidate);
    //}

    [Test]
    public void SopDoesNotMatch()
    {
        var scanner = new TestScanner(
           new TestScannerBaseOptions
           {
               SkipColumns = "SOPInstanceUID",
           },
           _fileSystem
        );

        var failureParts = scanner.Scan("1.2.392.200036.9116.2.6.1.48.1214834115.1486205112.923825");

        Assert.AreEqual(0, failureParts.Count);
    }
}
