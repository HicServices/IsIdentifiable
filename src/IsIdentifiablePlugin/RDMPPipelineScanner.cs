using IsIdentifiable.Failures;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Scanners;
using System;
using System.Data;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiablePlugin;

class RDMPPipelineScanner : ResourceScannerBase
{
    public RDMPPipelineScanner(
        RDMPPipelineScannerOptions options,
        IFileSystem fileSystem,
        params IFailureReport[] reports
    )
        : base(options, fileSystem, reports)
    {

    }

    public void Scan(DataTable dt)
    {
        var targetName = dt.TableName;

        foreach (DataRow row in dt.Rows)
        {
            foreach (DataColumn col in dt.Columns)
            {
                // validate some example data we might have fetched
                var val = row[col];
                var valAsString = val?.ToString();

                // null values cannot contain PII                
                if (val == DBNull.Value || valAsString == null)
                    continue;

                var badParts = Validate(col.ColumnName, valAsString).ToArray();

                // Pass all parts as a Failure to the destination reports
                if (badParts.Any())
                {
                    var f = new Failure(badParts)
                    {
                        ProblemField = col.ColumnName,
                        ProblemValue = valAsString,
                        Resource = targetName,
                    };

                    NotifyNewFailure(f);
                }
            }

            // Record progress
            NotifyDoneRows(1);
        }
    }

    protected override void DisposeImpl()
    {
        CloseReports();
    }

    protected override string LogProgressNoun() => "rows";
}
