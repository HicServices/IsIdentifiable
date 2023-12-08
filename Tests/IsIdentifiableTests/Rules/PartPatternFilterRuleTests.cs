using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using MongoDB.Driver.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Tests.Rules;

internal class PartPatternFilterRuleTests
{
    private static IEnumerable<string> TestCaseSource_ForamenMonroParts()
    {
        var parts = new List<string>();
        foreach (var prefix in new[] { "foramen", "foramina" })
        {
            foreach (var join in new[] { "of", "" })
            {
                foreach (var name in new[] { "monro", "monroe" })
                {
                    parts.Add(string.Join(" ", (new[] { prefix, join, name }).Where(x => !string.IsNullOrEmpty(x))));
                }
            }
        }
        return parts;
    }

    [TestCaseSource(nameof(TestCaseSource_ForamenMonroParts))]
    public void Covers_ForamenMonro(string valuePart)
    {
        // Arrange
        var rule = new PartPatternFilterRule()
        {
            IfPartPattern = "^Monroe?$",
            WordBefore = "(foramen|foramina)( of)?",
            IfColumn = "TextValue",
            As = FailureClassification.Person,
            Action = RuleAction.Ignore,
        };
        var name = valuePart.Split()[^1];
        var problemValue = $"Mr {name} has an issue with his {valuePart}";
        var validFailurePart = new FailurePart(name, FailureClassification.Person, 3);
        var problemOffset = problemValue.LastIndexOf(" ") + 1;
        var filteredFailurePart = new FailurePart(name, FailureClassification.Person, problemOffset);

        // Act
        var coversValidFailurePart = rule.Covers(validFailurePart, problemValue);
        var coversFilteredFailurePart = rule.Covers(filteredFailurePart, problemValue);

        // Assert
        Assert.False(coversValidFailurePart);
        Assert.True(coversFilteredFailurePart);
    }

    private static IEnumerable<string> TestCaseSource_HodgkinLymphomaParts()
    {
        var parts = new List<string>();
        foreach (var name in new[] { "hodgkin", "hodgkins", "hodgkin's" })
        {
            foreach (var postfix in new[] { "lymphoma", "disease" })
            {
                parts.Add(string.Join(" ", (new[] { name, postfix }).Where(x => !string.IsNullOrEmpty(x))));
            }
        }
        return parts;
    }

    [TestCaseSource(nameof(TestCaseSource_HodgkinLymphomaParts))]
    public void Covers_HodgkinLymphoma(string valuePart)
    {
        // Arrange
        var rule = new PartPatternFilterRule()
        {
            Action = RuleAction.Ignore,
            As = FailureClassification.Person,
            IfColumn = "TextValue",
            IfPartPattern = "^Hodgkin(s|'s)?$",
            WordAfter = "(lymphoma|disease|<br>lymphoma)",
        };
        var name = valuePart.Split()[0];
        var problemValue = $"Mr {name} possibly has {valuePart}";
        var validFailurePart = new FailurePart(name, FailureClassification.Person, 3);
        var problemOffset = problemValue.IndexOf($"has {name}") + 4;
        var filteredFailurePart = new FailurePart(name, FailureClassification.Person, problemOffset);

        // Act
        var coversValidFailurePart = rule.Covers(validFailurePart, problemValue);
        var coversFilteredFailurePart = rule.Covers(filteredFailurePart, problemValue);

        // Assert
        Assert.False(coversValidFailurePart);
        Assert.True(coversFilteredFailurePart);
    }

    [Test]
    public void Covers_HyphenInWordBefore()
    {
        // Arrange
        var rule = new PartPatternFilterRule()
        {
            IfPartPattern = "^Hodgkin$",
            WordBefore = "Non",
            IfColumn = "TextValue",
            As = FailureClassification.Person,
            Action = RuleAction.Ignore,
        };
        var problemValue = $"Non-Hodgkin's lymphoma";
        var failurePart = new FailurePart("Hodgkin", FailureClassification.Person, 4);

        // Act
        var ruleCoversFailurePart = rule.Covers(failurePart, problemValue);

        // Assert
        Assert.True(ruleCoversFailurePart);
    }

    [Test]
    public void Covers_HyphenInWordAfter()
    {
        // Arrange
        var rule = new PartPatternFilterRule()
        {
            IfPartPattern = "^Gr(a|e)y$",
            WordAfter = "white",
            IfColumn = "TextValue",
            As = FailureClassification.Person,
            Action = RuleAction.Ignore,
        };
        var problemValue = $"Gray-white foo";
        var failurePart = new FailurePart("Gray", FailureClassification.Person, 0);

        // Act
        var ruleCoversFailurePart = rule.Covers(failurePart, problemValue);

        // Assert
        Assert.True(ruleCoversFailurePart);
    }

    [Test]
    public void Covers_AnyFailureClassification()
    {
        // Arrange
        var rule = new PartPatternFilterRule()
        {
            IfPartPattern = "^Test$",
            IfColumn = "TextValue",
            Action = RuleAction.Ignore,
        };
        var problemValue = $"Test";
        var failurePart = new FailurePart("Test", FailureClassification.Person, 0);

        // Act
        var ruleCoversFailurePart = rule.Covers(failurePart, problemValue);

        // Assert
        Assert.True(ruleCoversFailurePart);
    }

    [Test]
    public void Constructor_WordBefore_StringStartOrEnd_ThrowsException()
    {
        Assert.Throws<ArgumentException>(
            () => new PartPatternFilterRule()
            {
                IfPartPattern = "^Test$",
                IfColumn = "TextValue",
                Action = RuleAction.Ignore,
                WordBefore = "^(2|3)",
            }
        );
    }

    [Test]
    public void Constructor_WordAfter_StringStartOrEnd_ThrowsException()
    {
        Assert.Throws<ArgumentException>(
            () => new PartPatternFilterRule()
            {
                IfPartPattern = "^Test$",
                IfColumn = "TextValue",
                Action = RuleAction.Ignore,
                WordAfter = "^(2|3)",
            }
        );
    }
}
