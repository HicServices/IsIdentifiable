using FAnsi.Discovery;
using IsIdentifiable.Options;
using System;

namespace IsIdentifiable.Util;

public static class DatabaseTargetHelpers
{
    public static DiscoveredServer GetDiscoveredServer(DatabaseTargetOptions options)
    {
        var databaseType = options.DatabaseType ?? throw new ArgumentException(nameof(options.DatabaseType));
        return new DiscoveredServer(options.DatabaseConnectionString, databaseType);
    }

    public static DiscoveredDatabase GetDiscoveredDatabase(DatabaseTargetOptions options)
    {
        var discoveredServer = GetDiscoveredServer(options);
        return discoveredServer.GetCurrentDatabase() ?? throw new Exception("No current database");
    }

    public static DiscoveredTable GetDiscoveredTable(DiscoveredDatabase database, string tableName)
    {
        var table = database.ExpectTable(tableName);
        if (!table.Exists())
            throw new Exception($"Table '{table}' does not exist in '{database.GetRuntimeName}'");
        return table;
    }

    public static DiscoveredTable GetDiscoveredTable(DatabaseTargetOptions options, string tableName)
    {
        var database = GetDiscoveredDatabase(options);
        return GetDiscoveredTable(database, tableName);
    }
}
