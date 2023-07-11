using IsIdentifiable.Rules;
using IsIdentifiable.Tests.TestUtil;
using IsIdentifiable.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests.Rules;

internal class YamlRuleStoreTests
{
    private MockFileSystem _fileSystem = null!;
    private readonly DateTimeProvider _dateTimeProvider = new TestDateTimeProvider();

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
    }

    [Test]
    public void Constructor_LoadsExistingRules()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t.yaml");
        using (var stream = fi.AppendText())
        {
            //todo serialize wrapper
            var content = string.Join(
                Environment.NewLine,
                new List<string>
                {
                        "- Action: Report",
                        "  IfPattern: foo",
                }
            );
            stream.Write(content);
        }

        var expected = new RegexRule
        {
            Action = RuleAction.Report,
            IfPattern = "foo"
        };

        // Act

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        // Assert

        Assert.AreEqual(store.RulesView.Count, 1);
        Assert.AreEqual(expected, store.RulesView[0]);
    }

    [Test]
    public void Add_NewRule_Single()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t");
        var store = new YamlRuleStore(fi, _dateTimeProvider);
        var rule = new RegexRule();
        var expected = RegexRuleHelpers.SerializeWithComment(rule, _dateTimeProvider.UtcNow());

        // Act

        store.Add(rule);
        store.Dispose();

        // Assert

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.AreEqual(expected, content);
    }

    [Test]
    public void Add_NewRule_Multiple()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t");
        var store = new YamlRuleStore(fi, _dateTimeProvider);
        var rule1 = new RegexRule() { IfColumn = "col1" };
        var expected1 = RegexRuleHelpers.SerializeWithComment(rule1, _dateTimeProvider.UtcNow());
        var rule2 = new RegexRule() { IfColumn = "col2" };
        var expected2 = RegexRuleHelpers.SerializeWithComment(rule2, _dateTimeProvider.UtcNow());

        // Act

        store.Add(rule1);
        store.Add(rule2);
        store.Flush();

        // Assert

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.AreEqual($"{expected1}{expected2}", content);
    }

    [Test]
    public void Delete_ExistingRuleFromFile()
    {
        // Arrange

        var rule = new RegexRule()
        {
            Action = RuleAction.Report,
            IfPattern = "foo",
        };

        var fi = _fileSystem.FileInfo.New("t.yaml");
        using (var stream = fi.AppendText())
        {
            stream.Write(RegexRuleHelpers.Serialize(rule));
        }

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        var expected = $"# Rule deleted by {Environment.UserName} - {_dateTimeProvider.UtcNow()}\r\n";

        // Act

        var deleted = store.Delete(rule);
        store.Flush();

        // Assert

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.True(deleted);
        Assert.AreEqual(expected, content);
    }

    [Test]
    public void Delete_NewRule()
    {
        // Arrange

        var rule = new RegexRule()
        {
            Action = RuleAction.Report,
            IfPattern = "foo",
        };

        var fi = _fileSystem.FileInfo.New("t.yaml");
        fi.Create();

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        // Act

        store.Add(rule);
        var deleted = store.Delete(rule);
        store.Flush();

        // Assert

        Assert.True(deleted);

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.True(content.Contains("# Rule deleted by"));
        Assert.False(content.Contains("IfPattern"));
    }

    [Test]
    public void Undo_NoRules_DoesNotThrow()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t.yaml");
        fi.Create();

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        // Act
        // Assert

        Assert.DoesNotThrow(() => store.Undo());
    }

    [Test]
    public void Undo_SingleRule()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t.yaml");
        fi.Create();

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        var rule = new RegexRule();
        store.Add(rule);

        // Act

        store.Undo();
        store.Flush();

        // Assert

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.True(content.Contains("# Rule deleted by"));
    }

    [Test]
    public void Undo_MultipleRules()
    {
        // Arrange

        var fi = _fileSystem.FileInfo.New("t.yaml");
        fi.Create();

        var store = new YamlRuleStore(fi, _dateTimeProvider);

        var rule1 = new RegexRule() { IfColumn = "foo" };
        store.Add(rule1);
        var rule2 = new RegexRule() { IfColumn = "bar" };
        store.Add(rule2);

        // Act

        store.Undo();
        store.Flush();

        // Assert

        var content = _fileSystem.File.ReadAllText(fi.FullName);
        Assert.True(content.Contains("# Rule deleted by"));
        Assert.True(content.Contains("foo"));
        Assert.False(content.Contains("bar"));
    }
}
