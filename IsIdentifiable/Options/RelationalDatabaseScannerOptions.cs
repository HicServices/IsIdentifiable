using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Options;

public sealed class RelationalDatabaseScannerOptions : ScannerBaseOptions
{
    [YamlMember(Alias = "Targets")]
    public List<DatabaseTargetOptions> DatabaseTargets { get; init; } = new();
}
