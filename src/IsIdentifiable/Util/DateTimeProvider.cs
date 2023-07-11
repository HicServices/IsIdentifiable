using System;

namespace IsIdentifiable.Util;

public class DateTimeProvider
{
    public virtual DateTime UtcNow() => DateTime.UtcNow;
}
