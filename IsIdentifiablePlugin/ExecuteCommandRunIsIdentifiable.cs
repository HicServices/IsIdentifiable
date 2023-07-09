using IsIdentifiable.Options;
using IsIdentifiable.Scanners;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data;
using ReusableLibraryCode.DataAccess;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace IsIdentifiablePlugin;

public class ExecuteCommandRunIsIdentifiable : BasicCommandExecution
{
    private readonly ICatalogue _catalogue;
    private FileInfo? _configYaml;
    private readonly ITableInfo? _table;

    public ExecuteCommandRunIsIdentifiable(
        IBasicActivateItems activator,
        ICatalogue catalogue,
        FileInfo? configYaml
    )
        : base(activator)
    {
        _catalogue = catalogue;
        _configYaml = configYaml;

        var tables = catalogue.GetTableInfosIdeallyJustFromMainTables();
        if (tables.Length != 1)
            SetImpossible("Catalogue draws from multiple tables so cannot be evaluated");
        else
            _table = tables[0];
    }

    public override void Execute()
    {
        base.Execute();

        if (_table == null)
            throw new Exception("No table picked to run on");

        _configYaml ??= BasicActivator.SelectFile("YAMLConfigFile", "YAML File", "*.yaml");

        // user cancelled
        if (_configYaml == null)
            return;

        var allOptions = YamlOptionsExtensions.Load<IsIdentifiableOptions>(new FileSystem().FileInfo.New(_configYaml.FullName));
        var relationalDatabaseScannerOptions = allOptions?.RelationalDatabaseScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RelationalDatabaseScannerOptions)} key", nameof(_configYaml));

        var server = _catalogue.GetDistinctLiveDatabaseServer(DataAccessContext.InternalDataProcessing, true);
        var databaseTargetOptions = new DatabaseTargetOptions
        {
            Name = "from-catalogue",
            DatabaseConnectionString = server.Builder.ConnectionString,
            DatabaseType = server.DatabaseType,
        };

        using var runner = new RelationalDatabaseScanner(relationalDatabaseScannerOptions, databaseTargetOptions, new FileSystem());
        using var cts = new CancellationTokenSource();

        BasicActivator.Wait("Evaluating Table", Task.Run(() =>
        {
            runner.Scan(_table.GetRuntimeName());
            runner.Dispose();
        }), cts);
    }
}
