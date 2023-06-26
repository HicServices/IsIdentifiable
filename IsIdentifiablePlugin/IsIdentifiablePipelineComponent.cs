using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting;
using IsIdentifiable.Runners;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System.Data;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiablePlugin;

/// <summary>
/// Pipeline component that validates data that is flowing through an RDMP
/// pipeline for PII (personally identifiable information)
/// </summary>
public class IsIdentifiablePipelineComponent : IDataFlowComponent<DataTable> , ICheckable, IPipelineOptionalRequirement<ExtractCommand>
{
    private CustomRunner? _runner;
    private string? _targetName;

// RDMP will handle this, dont complain that it isn't marked nullable
#pragma warning disable CS8618
    [DemandsInitialization("YAML file with the IsIdentifiable rules (regex, NLP, report formats etc)",Mandatory = true)]
    public string YamlConfigFile { get; set; }
#pragma warning restore CS8618
    public void Abort(IDataLoadEventListener listener)
    {
        
    }

    public void Check(ICheckNotifier notifier)
    {
        LoadYamlConfigFile();
        notifier.OnCheckPerformed(new CheckEventArgs($"Read YamlConfigFile successfully",CheckResult.Success));
    }

    public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
    {
        _runner?.Dispose();
    }

    public void PreInitialize(ExtractCommand value, IDataLoadEventListener listener)
    {
        // if we are being used in the context of data extraction then name the
        // report files by the name of the dataset/global being extracted
        _targetName = value.ToString();
    }

    public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        if(toProcess.Rows.Count > 0)
        {
            CreateRunner(_targetName ?? toProcess.TableName);

            if(_runner == null)
                throw new Exception("CreateRunner was called but no _runner was created");

            _runner.Run(toProcess);
        }

        return toProcess;
    }

    private void CreateRunner(string targetName)
    {
        if (_runner != null)
            return;
        
        var opts = LoadYamlConfigFile() 
            ?? throw new Exception("No options were loaded from yaml ");

        if(opts.IsIdentifiableOptions == null)
        {
            throw new Exception($"Yaml file did not contain IsIdentifiableOptions");
        }

        if (!string.IsNullOrWhiteSpace(targetName))
            opts.IsIdentifiableOptions.TargetName = targetName;

        _runner = new CustomRunner(opts.IsIdentifiableOptions, new FileSystem());
        
    }

    private GlobalOptions LoadYamlConfigFile()
    {

        var deserializer = new Deserializer();
        var opts = deserializer.Deserialize<GlobalOptions>(File.ReadAllText(YamlConfigFile));

        if (opts == null || opts.IsIdentifiableOptions == null)
            throw new Exception($"Yaml file {YamlConfigFile} did not contain IsIdentifiableOptions");
        
        return opts;
    }
}

class CustomRunner : IsIdentifiableAbstractRunner
{
    private readonly IsIdentifiableBaseOptions options;

    public CustomRunner(IsIdentifiableBaseOptions options, IFileSystem fileSystem)
        : base(options, fileSystem)
    {
        this.options = options;
    }
    public void Run(DataTable dt)
    {
        foreach (DataRow row in dt.Rows)
        {
            foreach (DataColumn col in dt.Columns)
            {
                // validate some example data we might have fetched
                var val = row[col];

                // null values cannot contain PII
                if (val == null || val == DBNull.Value)
                    continue;

                var badParts = Validate(col.ColumnName, val.ToString()).ToArray();

                // Pass all parts as a Failure to the destination reports
                if (badParts.Any())
                {
                    var f = new Failure(badParts)
                    {
                        ProblemField = col.ColumnName,
                        ProblemValue = val.ToString(),
                        Resource = options.GetTargetName(FileSystem)
                    };

                    AddToReports(f);
                }

            }

            // Record progress
            DoneRows(1);
        }
    }
    public override void Dispose()
    {
        // Once all data is finished being fetched, close the destination reports
        CloseReports();

        base.Dispose();
    }
    public override int Run()
    {
        throw new NotSupportedException();
    }
}
