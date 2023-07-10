using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Scanners;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace IsIdentifiable.Tests.RunnerTests;

public class DicomFileRunnerTest
{
    #region Fixture Methods

    private const string DataDirectory = @"../../../../../data/";
    private DirectoryInfo _tessDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var testRulesDir = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "IsIdentifiableRules"));
        testRulesDir.Create();

        _tessDir = new DirectoryInfo(Path.Combine(testRulesDir.Parent.FullName, "tessdata"));
        _tessDir.Create();
        var dest = Path.Combine(_tessDir.FullName, "eng.traineddata");
        if (!File.Exists(dest))
            File.Copy(Path.Combine(DataDirectory, "tessdata", "eng.traineddata"), dest);

    }

    [OneTimeTearDown]
    public void OneTimeTearDown() { }

    #endregion

    #region Test Methods

    [SetUp]
    public void SetUp() { }

    [TearDown]
    public void TearDown() { }

    #endregion

    #region Tests

    [TestCase(true)]
    [TestCase(false)]
    public void IgnorePixelDataLessThan(bool ignoreShortText)
    {
        var opts = new DicomFileScannerOptions
        {
            TessDirectory = _tessDir.FullName,

            // If we ignore less than 170 then only 1 bit of text should be reported.
            // NOTE(rkm 2020-11-16) The test image should report 3 bits of text with lengths 123, 127, and 170.
            IgnoreTextLessThan = ignoreShortText ? 170 : 0
        };

        var fileSystem = new System.IO.Abstractions.FileSystem();

        var fileName = Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(DicomFileRunnerTest), "f1.dcm");
        TestData.Create(fileSystem.FileInfo.New(fileName), TestData.BURNED_IN_TEXT_IMG);

        var memoryReport = new ToMemoryFailureReport();
        var scanner = new DicomFileScanner(opts, fileSystem, memoryReport);

        var fileInfo = fileSystem.FileInfo.New(fileName);
        Assert.True(fileInfo.Exists);

        scanner.Scan(fileInfo);

        var failures = memoryReport.Failures.ToList();
        Assert.AreEqual(ignoreShortText ? 1 : 3, failures.Count);
    }

    #endregion
}
