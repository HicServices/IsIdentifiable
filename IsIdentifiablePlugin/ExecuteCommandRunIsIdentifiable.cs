using IsIdentifiable.Options;
using IsIdentifiable.Runners;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data;
using ReusableLibraryCode.DataAccess;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiablePlugin;

internal class ExecuteCommandRunIsIdentifiable : BasicCommandExecution
{
    private readonly ICatalogue _catalogue;
    private readonly FileInfo? _configYaml;
    private readonly ITableInfo? _table;

    public ExecuteCommandRunIsIdentifiable(IBasicActivateItems activator, ICatalogue catalogue, FileInfo? configYaml ) : base(activator)
    {
        this._catalogue = catalogue;
        this._configYaml = configYaml;

        var tables = catalogue.GetTableInfosIdeallyJustFromMainTables();
        if(tables.Length != 1)
        {
            SetImpossible("Catalogue draws from multiple tables so cannot be evaluated");
        }
        else
        {
            this._table = tables[0];
        }        
    }
    public override void Execute()
    {
        base.Execute();

        var file = _configYaml;
        
        if(_table == null)
            throw new System.Exception("No table picked to run on");

        file ??= BasicActivator.SelectFile("YAMLConfigFile", "YAML File", "*.yaml");

        // user cancelled
        if (file == null)
            return;

        var dbOpts = new IsIdentifiableRelationalDatabaseOptions();

        var deserializer = new Deserializer();
        var baseOptions = deserializer.Deserialize<GlobalOptions>(File.ReadAllText(file.FullName));

        if (baseOptions?.IsIdentifiableOptions == null)
            throw new Exception($"Yaml file did not contain IsIdentifiableOptions");

        // use the settings in the users file
        dbOpts.InheritValuesFrom(baseOptions.IsIdentifiableOptions);

        var server = _catalogue.GetDistinctLiveDatabaseServer(DataAccessContext.InternalDataProcessing, true);

        // but the connection strings from the Catalogue
        dbOpts.DatabaseConnectionString = server.Builder.ConnectionString;
        dbOpts.DatabaseType = server.DatabaseType;
        dbOpts.TableName = _table.GetRuntimeName();

        var runner = new DatabaseRunner(dbOpts, new FileSystem());
        using var cts = new CancellationTokenSource();

        BasicActivator.Wait("Evaluating Table", Task.Run(()=>
        {
            runner.Run();
            runner.Dispose();
        }), cts);
    }
}
