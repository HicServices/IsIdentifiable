using IsIdentifiable.Options;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Data;
using System.IO.Abstractions;

namespace IsIdentifiablePlugin;

/// <summary>
/// Pipeline component that validates data that is flowing through an RDMP
/// pipeline for PII (personally identifiable information)
/// </summary>
public class IsIdentifiablePipelineComponent : IDataFlowComponent<DataTable>, ICheckable, IPipelineOptionalRequirement<ExtractCommand>
{

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [DemandsInitialization("YAML file with the IsIdentifiable rules (regex, NLP, report formats etc)", Mandatory = true)]
    public string YamlConfigFile { get; set; }
#pragma warning restore CS8618

    private static readonly IFileSystem _fileSystem = new FileSystem();
    private RDMPPipelineScanner? _scanner;
    private RDMPPipelineScannerOptions? _options;

    public void Abort(IDataLoadEventListener listener) { }

    public void Check(ICheckNotifier notifier)
    {
        LoadConfigFile();
        notifier.OnCheckPerformed(new CheckEventArgs($"Read YamlConfigFile successfully", CheckResult.Success));
    }

    public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny) => _scanner?.Dispose();

    public void PreInitialize(ExtractCommand value, IDataLoadEventListener listener) { }

    public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        if (toProcess.Rows.Count <= 0)
            return toProcess;

        _scanner ??= GetScanner();
        _scanner.Scan(toProcess);

        return toProcess;
    }

    private RDMPPipelineScanner GetScanner()
    {
        if (_options == null)
            LoadConfigFile();

        return new RDMPPipelineScanner(_options!, _fileSystem);
    }

    private void LoadConfigFile()
    {
        var fi = _fileSystem.FileInfo.New(YamlConfigFile);
        var allOptions = YamlOptionsExtensions.Load<IsIdentifiableRDMPOptions>(fi);

        _options = allOptions?.RDMPPipelineScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RDMPPipelineScannerOptions)} key", nameof(YamlConfigFile));
    }
}
