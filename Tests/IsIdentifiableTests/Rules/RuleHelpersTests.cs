using IsIdentifiable.Rules;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace IsIdentifiable.Tests.Rules
{
    public class RuleHelpersTests
    {
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
                $"#Ruairidh - {now}",
                "- Action: Report",
                "  IfPattern: foo",
                "",
            };

            // Act
            var serialized = RuleHelpers.SerializeWithComment(rule, now);

            // Assert
            Assert.AreEqual(string.Join(Environment.NewLine, expectedLines), serialized);
        }
    }
}
