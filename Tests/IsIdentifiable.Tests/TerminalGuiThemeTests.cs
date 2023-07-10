using ii;
using NUnit.Framework;
using Terminal.Gui;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Tests;

class TerminalGuiThemeTests
{
    [Test]
    public void TestDeserialization()
    {
        var themeFile = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "theme.yaml");
        var des = new Deserializer();

        var theme = des.Deserialize<TerminalGuiTheme>(System.IO.File.ReadAllText(themeFile));

        Assert.AreNotEqual(default(Color), theme.Base.HotFocusBackground);
        Assert.AreNotEqual(default(Color), theme.Base.HotFocusForeground);
        Assert.AreEqual(Color.Black, theme.Base.FocusForeground);
        Assert.AreNotEqual(default(Color), theme.Base.FocusBackground);
        Assert.AreNotEqual(default(Color), theme.Base.HotNormalBackground);
        Assert.AreNotEqual(default(Color), theme.Base.HotNormalForeground);

        theme = new TerminalGuiTheme();

        Assert.AreEqual(default(Color), theme.Base.HotFocusBackground);
        Assert.AreEqual(default(Color), theme.Base.HotFocusForeground);
        Assert.AreEqual(default(Color), theme.Base.FocusForeground);
        Assert.AreEqual(default(Color), theme.Base.FocusBackground);
        Assert.AreEqual(default(Color), theme.Base.HotNormalBackground);
        Assert.AreEqual(default(Color), theme.Base.HotNormalForeground);

    }
}
