using System;

namespace IsIdentifiable.Helpers
{
    public class DateTimeProvider
    {
        public virtual DateTime UtcNow() => DateTime.UtcNow;
    }
}
