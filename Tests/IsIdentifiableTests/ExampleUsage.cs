using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Runners;
using NUnit.Framework;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;

namespace IsIdentifiable.Tests;

internal class ExampleUsage
{
    class CustomRunner : RunnerBase
    {
        public CustomRunner(IFailureReport report, MockFileSystem fileSystem)
            : base(new IsIdentifiableOptions(), fileSystem, report)
        { }

        public override int Run()
        {
            var field = "SomeText";
            var content = "Patient DoB is 2Mar he is my best buddy. CHI number is 0101010101";

            // validate some example data we might have fetched
            var badParts = Validate(field, content);

            // You can ignore or adjust these badParts if you want before passing to destination reports
            var failureParts = badParts.ToList();
            if (failureParts.Any())
            {
                var f = new Failure(failureParts)
                {
                    ProblemField = field,
                    ProblemValue = content,
                };

                // Pass all parts as a Failure to the destination reports
                AddToReports(f);
            }

            // Record progress
            DoneRows(1);

            // Once all data is finished being fetched, close the destination reports
            CloseReports();

            return 0;
        }
    }
    [Test]
    public void ExampleUsageOfIsIdentifiable()
    {
        // Where to put the output, in this case just to memory
        var dest = new ToMemoryFailureReport();

        // Your runner that fetches and validates data
        var runner = new CustomRunner(dest, new MockFileSystem());

        // fetch and analyise data
        runner.Run();

        Assert.AreEqual(1, dest.Failures.Count);
        Assert.AreEqual(2, dest.Failures[0].Parts.Count);
        Assert.AreEqual("2Mar", dest.Failures[0].Parts[0].Word);
        Assert.AreEqual("0101010101", dest.Failures[0].Parts[1].Word);
    }
}
