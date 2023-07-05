using System.IO.Abstractions;

namespace IsIdentifiable.Scanners;

public interface IFileScanner : IResourceScanner
{
    /// <summary>
    /// Scans for identifiable information in the specified <paramref name="file"/>
    /// </summary>
    /// <param name="file"></param>
    void Scan(IFileInfo file);
}
