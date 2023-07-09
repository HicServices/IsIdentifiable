using FAnsi;
using ii.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Options;

public class DatabaseTargetOptionsExtensions
{
    public static DatabaseTargetOptions OptionsFrom(
        IList<DatabaseTargetOptions> baseOptions,
        IDatabaseTargetOptions overrideOptions,
        string fallbackName
    )
    {
        DatabaseTargetOptions databaseTargetOptions;

        if (overrideOptions.TargetDatabaseName != null)
        {
            databaseTargetOptions =
                baseOptions.FirstOrDefault(t => string.Equals(t.Name, overrideOptions.TargetDatabaseName, StringComparison.CurrentCultureIgnoreCase)) ??
                throw new ArgumentException($"Yaml file did not contain the specified database {overrideOptions.TargetDatabaseName}");
        }
        else
        {
            if (!Enum.TryParse<DatabaseType>(overrideOptions.DatabaseType, ignoreCase: true, out var dbType))
                throw new ArgumentException($"Could not interpret '{overrideOptions.DatabaseType}' as a {typeof(DatabaseType)}");

            databaseTargetOptions = new DatabaseTargetOptions
            {
                Name = fallbackName,
                DatabaseConnectionString = overrideOptions.DatabaseConnectionString,
                DatabaseType = dbType,
            };
        }

        return databaseTargetOptions;
    }
}
