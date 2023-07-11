using IsIdentifiable.Util;
using System;

namespace IsIdentifiable.Tests.TestUtil;

public class TestDateTimeProvider : DateTimeProvider
{
    private readonly DateTime _instance;

    public TestDateTimeProvider()
    {
        _instance = DateTime.UtcNow;
    }

    public override DateTime UtcNow() => _instance;
}
