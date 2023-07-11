using System;
using System.Collections.Generic;

namespace IsIdentifiable.AllowLists;

/// <summary>
/// Interface for classes that produce a list of values which when
/// detected as 'identifiable information' should in fact instead
/// be ignored.
/// </summary>
public interface IAllowListSource : IDisposable
{
    /// <summary>
    /// Return all unique strings which should be ignored.  These strings should be trimmed.  Case is not relevant.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAllowList();
}
