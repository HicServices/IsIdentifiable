using CsvHelper.Configuration;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Destinations;
using IsIdentifiable.Reporting.Reports;
using NUnit.Framework;
using System;
using System.Data;
using System.Globalization;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests;

internal class TestDestinations
{
    private MockFileSystem _fileSystem = null!;
    private const string OUT_DIR = "test";

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }

    [Test]
    public void TestCsvDestination_Normal()
    {
        var outputFileInfo = _fileSystem.FileInfo.New(_fileSystem.Path.Join(OUT_DIR, "test.csv"));
        var dest = new CsvDestination(new CsvReportDestinationOptions(), outputFileInfo);

        var report = new TestFailureReport(dest);
        report.WriteToDestinations();
        report.CloseReport();

        var fileCreatedContents = _fileSystem.File.ReadAllText(outputFileInfo.FullName);
        fileCreatedContents = fileCreatedContents.Replace("\r\n", Environment.NewLine);

        TestHelpers.AreEqualIgnoringLineEndings(@"col1,col2
""cell1 with some new 
 lines and 	 tabs"",cell2
", fileCreatedContents);
    }

    [Test]
    public void TestCsvDestination_NormalButNoWhitespace()
    {
        var outputFileInfo = _fileSystem.FileInfo.New(_fileSystem.Path.Join(OUT_DIR, "test.csv"));
        var dest = new CsvDestination(
            new CsvReportDestinationOptions
            {
                StripWhitespaceOnWrite = true,
            },
            outputFileInfo
        );

        var report = new TestFailureReport(dest);
        report.WriteToDestinations();
        report.CloseReport();

        var fileCreatedContents = _fileSystem.File.ReadAllText(outputFileInfo.FullName);
        fileCreatedContents = fileCreatedContents.Replace("\r\n", Environment.NewLine);

        TestHelpers.AreEqualIgnoringLineEndings(@"col1,col2
cell1 with some new lines and tabs,cell2
", fileCreatedContents);
    }

    [Test]
    public void TestCsvDestination_Tabs()
    {
        var outputFileInfo = _fileSystem.FileInfo.New(_fileSystem.Path.Join(OUT_DIR, "test.csv"));
        var dest = new CsvDestination(
            new CsvReportDestinationOptions
            {
                // This is slash t, not an tab
                CsvSeparator = "\\t",
                StripWhitespaceOnWrite = true,
            },
            outputFileInfo
        );

        var report = new TestFailureReport(dest);
        report.WriteToDestinations();
        report.CloseReport();

        var fileCreatedContents = _fileSystem.File.ReadAllText(outputFileInfo.FullName);
        fileCreatedContents = fileCreatedContents.Replace("\r\n", Environment.NewLine);

        TestHelpers.AreEqualIgnoringLineEndings(@"col1	col2
cell1 with some new lines and tabs	cell2
", fileCreatedContents);
    }

    [Test]
    public void CsvDestination_WithCsvConfiguration()
    {
        var outputFileInfo = _fileSystem.FileInfo.New(_fileSystem.Path.Join(OUT_DIR, "test.csv"));
        var conf = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = "|",
        };

        using (var dest = new CsvDestination(new CsvReportDestinationOptions(), outputFileInfo, conf))
        {
            dest.WriteHeader("foo", "bar");
        }

        var fileCreatedContents = _fileSystem.File.ReadAllText(outputFileInfo.FullName);
        Assert.True(fileCreatedContents.StartsWith("foo|bar"));
    }
}

internal class TestFailureReport : IFailureReport
{
    private readonly IReportDestination _dest;

    private readonly DataTable _dt = new();

    public TestFailureReport(IReportDestination dest)
    {
        _dest = dest;

        _dt.Columns.Add("col1");
        _dt.Columns.Add("col2");
        _dt.Rows.Add("cell1 with some new \r\n lines and \t tabs", "cell2");
    }


    public void AddDestinations(IsIdentifiableOptions options) { }

    public void DoneRows(int numberDone) { }

    public void Add(Failure failure) { }

    public void CloseReport()
    {
        _dest.Dispose();
    }

    public void WriteToDestinations()
    {
        _dest.WriteItems(_dt);
    }
}
