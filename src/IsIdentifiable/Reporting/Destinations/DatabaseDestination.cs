using FAnsi.Discovery;
using System;
using System.Data;

namespace IsIdentifiable.Reporting.Destinations;

internal class DatabaseDestination : ReportDestination
{
    private readonly string _reportName;
    private readonly DiscoveredTable _tbl;

    public DatabaseDestination(DatabaseReportDestinationOptions options, string reportName)
        : base(options)
    {
        if (options.DestinationDatabaseType == null)
            throw new Exception($"{nameof(DatabaseReportDestinationOptions.DestinationDatabaseType)} must be specified to use this destination (it was null)");

        var targetDatabase = new DiscoveredServer(options.DestinationConnectionString, options.DestinationDatabaseType.Value).GetCurrentDatabase();
        if (!targetDatabase.Exists())
            throw new Exception("Destination database did not exist");

        _tbl = targetDatabase.ExpectTable(reportName);
        if (_tbl.Exists())
            _tbl.Drop();

        _reportName = reportName;
    }

    public override void WriteItems(DataTable items)
    {
        StripWhiteSpace(items);

        items.TableName = _reportName;

        if (!_tbl.Exists())
            _tbl.Database.CreateTable(_tbl.GetRuntimeName(), items);
        else
        {
            using var insert = _tbl.BeginBulkInsert();
            insert.Upload(items);
        }
    }

    private void StripWhiteSpace(DataTable items)
    {
        if (!Options.StripWhitespaceOnWrite)
            return;

        foreach (DataRow row in items.Rows)
            foreach (DataColumn col in items.Columns)
                row[col] = StripWhitespace(row[col]);
    }
}
