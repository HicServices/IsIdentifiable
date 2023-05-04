using IsIdentifiable.Rules;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace IsIdentifiable.Tests.Rules
{
    public class RuleHelpersTests
    {
        private MockFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
        }

        [Test]
        public void Serialize_RegexRule()
        {
            // Arrange
            var rule = new RegexRule()
            {
                IfPattern = "foo",
                Action = RuleAction.Report,
            };

            var expectedLines = new List<string> {
                "- Action: Report",
                "  IfPattern: foo",
                "",
            };

            // Act
            var serialized = RuleHelpers.Serialize(rule);

            // Assert
            Assert.AreEqual(string.Join(Environment.NewLine, expectedLines), serialized);
        }

        [Test]
        public void SerializeWithComment_RegexRule()
        {
            // Arrange
            var rule = new RegexRule()
            {
                IfPattern = "foo",
                Action = RuleAction.Report,
            };

            var now = DateTime.Now;
            var expectedLines = new List<string> {
                $"#{Environment.UserName} - {now}",
                "- Action: Report",
                "  IfPattern: foo",
                "",
            };

            // Act
            var serialized = RuleHelpers.SerializeWithComment(rule, now);

            // Assert
            Assert.AreEqual(string.Join(Environment.NewLine, expectedLines), serialized);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LoadFrom_MissingFile_CreateIfMissing(bool createIfMissing)
        {
            // Arrange
            var fi = _fileSystem.FileInfo.New("t.yaml");

            if (createIfMissing)
            {
                // Act
                RuleHelpers.LoadFrom(fi, createIfMissing);

                // Assert
                Assert.IsTrue(fi.Exists);
            }
            else
            {
                // Act
                // Assert
                Assert.Throws(
                    Is.TypeOf<FileNotFoundException>()
                    .And.Message.Contains("t.yaml does not exist"),
                    () => RuleHelpers.LoadFrom(fi, createIfMissing)
                );
            }
        }

        [Test]
        public void LoadFrom_ExistingFile_Empty()
        {
            // Arrange
            var fi = _fileSystem.FileInfo.New("t.yaml");
            fi.Create();

            // Act
            var rules = RuleHelpers.LoadFrom(fi, createIfMissing: false);

            // Assert
            Assert.AreEqual(new List<IRegexRule>(), rules);
        }

        [Test]
        public void LoadFrom_ExistingFile_InvalidContent()
        {
            // Arrange
            var fi = _fileSystem.FileInfo.New("t.md");
            using (var stream = fi.AppendText())
            {
                stream.Write("these are not the rules you're looking for");
            }

            // Act
            // Assert
            Assert.Throws(
                Is.TypeOf<InvalidDataException>()
                .And.Message.Contains("Invalid rules content in file")
                .And.Message.Contains("t.md"),
                () => RuleHelpers.LoadFrom(fi, createIfMissing: false)
            );
        }

        [Test]
        public void LoadFrom_ExistingFile_WithRules()
        {
            // Arrange
            var fi = _fileSystem.FileInfo.New("t.yaml");
            using (var stream = fi.AppendText())
            {
                // todo serialize wrapper
                var content = string.Join(
                    Environment.NewLine,
                    new List<string>
                    {
                        $"# a comment",
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
            var rules = RuleHelpers.LoadFrom(fi, createIfMissing: false);

            // Assert
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(expected, rules[0]);
        }
    }
}
