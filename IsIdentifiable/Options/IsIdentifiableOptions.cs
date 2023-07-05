using System;
using System.IO.Abstractions;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public class IsIdentifiableOptions
{
    [YamlMember(Alias = "DicomFileScanner")]
    public DicomFileScannerOptions? DicomFileScannerOptions { get; init; }

    [YamlMember(Alias = "RelationalDatabaseScanner")]
    public RelationalDatabaseScannerOptions? RelationalDatabaseScannerOptions { get; init; }

    [YamlMember(Alias = "MongoDBScanner")]
    public MongoDBScannerOptions? MongoDBScannerOptions { get; init; }

    [YamlMember(Alias = "CSVFile")]
    public CSVFileScannerOptions? CSVFileScannerOptions { get; init; }

    public static IsIdentifiableOptions Load(IFileInfo fileInfo) => Load<IsIdentifiableOptions>(fileInfo);

    public static T Load<T>(IFileInfo fileInfo) where T : IsIdentifiableOptions
    {
        var yamlOptions = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<T>(
                fileInfo.OpenText().ReadToEnd()
            );

        if (yamlOptions == null)
            throw new ArgumentException($"Could not find key '{YamlOptions.IS_IDENTIFIABLE_YAML_KEY}' in file");

        return yamlOptions;
    }
}
