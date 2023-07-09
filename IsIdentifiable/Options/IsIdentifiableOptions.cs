using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public class IsIdentifiableOptions
{
    [YamlMember(Alias = "DatabaseTargets")]
    public List<DatabaseTargetOptions> DatabaseTargets { get; init; } = new();

    [YamlMember(Alias = "DicomFileScanner")]
    public DicomFileScannerOptions? DicomFileScannerOptions { get; init; }

    [YamlMember(Alias = "RelationalDatabaseScanner")]
    public RelationalDatabaseScannerOptions? RelationalDatabaseScannerOptions { get; init; }

    [YamlMember(Alias = "MongoDBScanner")]
    public MongoDBScannerOptions? MongoDBScannerOptions { get; init; }

    [YamlMember(Alias = "CSVFile")]
    public CSVFileScannerOptions? CSVFileScannerOptions { get; init; }
}
