using NUnit.Framework;
using System.IO.Abstractions;

namespace IsIdentifiable.Tests.TestUtil;

public sealed class TestData
{
    // Paths to the test DICOM files relative to TestContext.CurrentContext.TestDirectory
    // TODO(rkm 2020-11-16) Enum-ify these members so they can be strongly-typed instead of stringly-typed
    private const string TEST_DATA_DIR = "TestData";
    public static string IMG_013 = System.IO.Path.Combine(TEST_DATA_DIR, "IM-0001-0013.dcm");
    public static string IMG_019 = System.IO.Path.Combine(TEST_DATA_DIR, "IM-0001-0019.dcm");
    public static string IMG_024 = System.IO.Path.Combine(TEST_DATA_DIR, "IM-0001-0024.dcm");
    public static string MANY_TAGS = System.IO.Path.Combine(TEST_DATA_DIR, "FileWithLotsOfTags.dcm");
    public static string INVALID_DICOM = System.IO.Path.Combine(TEST_DATA_DIR, "NotADicomFile.txt");
    public static string BURNED_IN_TEXT_IMG = System.IO.Path.Combine(TEST_DATA_DIR, "burned-in-text-test.dcm");

    /// <summary>
    /// Creates the test image <see cref="IMG_013"/> in the file location specified
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="testFile">The test file to create, should be a static member of this class.  Defaults to <see cref="IMG_013"/></param>
    /// <returns></returns>
    public static IFileInfo Create(IFileInfo dest, string testFile = null)
    {
        var from = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, testFile ?? IMG_013);
        var bytes = System.IO.File.ReadAllBytes(from);

        if (dest.Directory?.Exists == false)
            dest.Directory.Create();

        using var stream = dest.OpenWrite();
        stream.Write(bytes);

        return dest;
    }
}
