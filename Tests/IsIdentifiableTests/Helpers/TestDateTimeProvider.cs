using IsIdentifiable.Helpers;
using System;

namespace IsIdentifiable.Tests.Helpers
{
    public class TestDateTimeProvider : DateTimeProvider
    {
        private readonly DateTime _instance;

        public TestDateTimeProvider()
        {
            _instance = DateTime.UtcNow;
        }

        public override DateTime UtcNow() => _instance;
    }
}
