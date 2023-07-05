using FAnsi;

namespace IsIdentifiable.Reporting.Destinations;

public class DatabaseReportDestinationOptions : ReportDestinationOptions
{
    /// <summary>
    /// Full connection string to the database in which to store the report results
    /// </summary>
    public string? DestinationConnectionString { get; set; }

    /// <summary>
    /// The DBMS provider of DestinationConnectionString e.g. MySql
    /// </summary>
    public DatabaseType? DestinationDatabaseType { get; set; }
}
