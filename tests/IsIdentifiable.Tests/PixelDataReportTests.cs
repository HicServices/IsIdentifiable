using IsIdentifiable.Failures;
using IsIdentifiable.Redacting;
using NUnit.Framework;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Text.RegularExpressions;
using System.Threading;

namespace IsIdentifiable.Tests;

internal class PixelDataReportTests
{
    private MockFileSystem _fileSystem;
    private const string _pixelDataReportPath = "pixeldatareport.csv";
    private byte[] _pixelDataReportData;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _pixelDataReportData = System.IO.File.ReadAllBytes(
            System.IO.Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                _pixelDataReportPath
            )
        );
    }

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
        _fileSystem.File.WriteAllBytes(_pixelDataReportPath, _pixelDataReportData);
    }


    [Test]
    public void TestReportReader_PixelReport()
    {
        var reader = new ReportReader(_fileSystem.FileInfo.New(_pixelDataReportPath), (s) => { }, _fileSystem, CancellationToken.None);
        Assert.IsNotEmpty(reader.Failures);
    }


    [Test]
    public void MatchProblemValuesPatternFactory_PixelReport()
    {
        var reader = new ReportReader(_fileSystem.FileInfo.New(_pixelDataReportPath), (s) => { }, _fileSystem, CancellationToken.None);

        foreach (var l in reader.Failures)
        {
            new MatchProblemValuesPatternFactory().GetPattern(this, l);
        }
    }

    [Test]
    public void IgnoreRuleGenerator_PixelReport()
    {
        var f = GetPixelFailure(out var ocrOutput);
        var g = new IgnoreRuleGenerator(_fileSystem);
        Assert.AreEqual($"^{Regex.Escape(ocrOutput)}$", g.RulesFactory.GetPattern(this, f), "When the user ignores OCR data the ignore pattern should exactly match the full text discovered");
    }

    [Test]
    public void UpdateRuleGenerator_PixelReport()
    {
        var f = GetPixelFailure(out var ocrOutput);
        var g = new RowUpdater(_fileSystem);
        Assert.AreEqual($"^{Regex.Escape(ocrOutput)}$", g.RulesFactory.GetPattern(this, f), "When the user marks problematic the OCR data the pattern should exactly match the full text discovered");
    }

    [TestCase(typeof(SymbolsRulesFactory))]
    [TestCase(typeof(MatchProblemValuesPatternFactory))]
    [TestCase(typeof(MatchWholeStringRulePatternFactory))]
    public void SymbolsRulesFactory_PixelReport(Type factoryType)
    {
        var f = GetPixelFailure(out var ocrOutput);
        var factory = (IRulePatternFactory)Activator.CreateInstance(factoryType);
        Assert.AreEqual($"^{Regex.Escape(ocrOutput)}$", factory.GetPattern(this, f), "All pattern generators should just return the full string for OCR data");
    }


    private static Failure GetPixelFailure(out string ocrOutput)
    {
        ocrOutput = @"12  w ! i  e  16  17  it  19  P B \ B K e N 10 iR 0 ) f  LB 1 1 1 1 LB 1 1 G w 3 In 1     =2  P  4  22 i  P     P        INRINET] INRINET] IARINEN] INRINET] INRINET] INRINET] INRINET] INRINEN]  028 PLe 30 e e 33 2y 35 1] O L @ L @ L G L 6 1] @ L @ L  INRINET] INRINET] INRINET] INRINET] INRINET] INRINEN] ITNINET     @  Bl  Y4  o  it        m  e  Gx     45  2";

        return new Failure(new[] { new FailurePart(ocrOutput, FailureClassification.PixelText, -1) })
        {
            Resource = "/home/thomas/testdicoms2/gdcmConformanceTests/CT_OSIRIX_OddOverlay.dcm",
            ResourcePrimaryKey = "1.3.12.2.1107.5.1.4.51771.30050005122714151602300000095",
            ProblemField = "PixelData",
            ProblemValue = ocrOutput
        };
    }
}
