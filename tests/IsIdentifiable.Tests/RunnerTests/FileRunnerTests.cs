using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Scanners;
using Moq;
using NUnit.Framework;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests.RunnerTests;

class FileRunnerTests
{
    private MockFileSystem _fileSystem = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }


    [Test]
    public void FileRunner_CsvWithCHI()
    {
        var fi = _fileSystem.FileInfo.New("testfile.csv");
        using (var s = fi.CreateText())
        {
            s.WriteLine("Fish,Chi,Bob");
            s.WriteLine("123,0102821172,32 Ankleberry lane");
            s.Flush();
            s.Close();
        }

        var reporter = new Mock<IFailureReport>(MockBehavior.Strict);

        reporter.Setup(f => f.Add(It.IsAny<Failure>())).Callback<Failure>(f => Assert.AreEqual("0102821172", f.ProblemValue));
        reporter.Setup(f => f.DoneRows(1));
        reporter.Setup(f => f.CloseReport());

        var scanner = new CsvFileScanner(
            new CSVFileScannerOptions(),
            _fileSystem,
            stopAfter: 0,
            reporter.Object
        );

        scanner.Scan(fi);

        reporter.Verify();
    }

    [Test]
    public void FileRunner_TopX()
    {
        var fi = _fileSystem.FileInfo.New("testfile.csv");
        using (var s = fi.CreateText())
        {
            s.WriteLine("Fish,Chi,Bob");

            // create a 100 line file
            for (var i = 0; i < 100; i++)
                s.WriteLine("123,0102821172,32 Ankleberry lane");

            s.Flush();
            s.Close();
        }

        var done = 0;
        var reporter = new Mock<IFailureReport>(MockBehavior.Strict);
        reporter.Setup(f => f.Add(It.IsAny<Failure>())).Callback<Failure>(f => Assert.AreEqual("0102821172", f.ProblemValue));
        reporter.Setup(f => f.DoneRows(1)).Callback(() => done++);
        reporter.Setup(f => f.CloseReport());

        var scanner = new CsvFileScanner(
            new CSVFileScannerOptions(),
            _fileSystem,
            stopAfter: 22,
            reporter.Object
        );

        scanner.Scan(fi);

        reporter.Verify();
        Assert.AreEqual(22, done);
    }
}
