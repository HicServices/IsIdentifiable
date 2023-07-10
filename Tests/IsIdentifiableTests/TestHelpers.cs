using NUnit.Framework;
using System;

namespace IsIdentifiable.Tests;

public class TestHelpers
{
    // Assert two strings match apart from line endings
    public static void AreEqualIgnoringCaseAndLineEndings(string a, string b)
    {
        StringAssert.AreEqualIgnoringCase(a.Replace("\r\n", "\n"), b.Replace("\r\n", "\n"));
    }

    // Assert two strings match apart from line endings, case sensitive
    public static void AreEqualIgnoringLineEndings(string a, string b)
    {
        StringAssert.AreEqualIgnoringCase(a.Replace("\r\n", "\n"), b.Replace("\r\n", "\n"));
    }

    public static void Contains(string needle, string haystack)
    {
        StringAssert.Contains(needle.Replace("\r\n", "\n"), haystack.Replace("\r\n", "\n"));
    }

    public static void DoesNotContain(string needle, string haystack)
    {
        StringAssert.DoesNotContain(needle.Replace("\r\n", "\n"), haystack.Replace("\r\n", "\n"));
    }

    internal static string EnvironmentStringJoin(params string[] lines) => string.Join(Environment.NewLine, lines);
}
