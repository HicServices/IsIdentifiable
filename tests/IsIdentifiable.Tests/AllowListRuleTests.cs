using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using NUnit.Framework;

namespace IsIdentifiable.Tests;

class AllowlistRuleTests
{

    [Test]
    public void TestAllowlistRule_IfPattern_CaseSensitivity()
    {
        var rule = new AllowlistRule
        {
            //Ignore any failure where any of the input string matches "fff"
            IfPattern = "fff"
        };

        Assert.IsFalse(rule.CaseSensitive);

        Assert.AreEqual(
            RuleAction.Ignore, rule.ApplyAllowlistRule("aba", "FFF Troll", new FailurePart("Troll", FailureClassification.Location, 0)));

        rule.CaseSensitive = true;

        Assert.AreEqual(
            RuleAction.None, rule.ApplyAllowlistRule("aba", "FFF Troll", new FailurePart("Troll", FailureClassification.Location, 0)));
    }

    [Test]
    public void TestAllowlistRule_IfPartPattern_CaseSensitivity()
    {
        var rule = new AllowlistRule
        {
            //Ignore any failure the specific section that is bad is this:
            IfPartPattern = "^troll$"
        };

        Assert.IsFalse(rule.CaseSensitive);

        Assert.AreEqual(
            RuleAction.Ignore, rule.ApplyAllowlistRule("aba", "FFF Troll", new FailurePart("Troll", FailureClassification.Location, 0)));

        rule.CaseSensitive = true;

        Assert.AreEqual(
            RuleAction.None, rule.ApplyAllowlistRule("aba", "FFF Troll", new FailurePart("Troll", FailureClassification.Location, 0)));
    }

    [Test]
    public void TestAllowlistRule_As()
    {
        var rule = new AllowlistRule
        {
            //Ignore any failure the specific section that is bad is this:
            IfPartPattern = "^troll$",
            As = FailureClassification.Person
        };


        Assert.AreEqual(
            RuleAction.None, rule.ApplyAllowlistRule("aba", "FFF Troll",
                new FailurePart("Troll", FailureClassification.Location, 0)), "Rule should not apply when FailureClassification is Location");

        Assert.AreEqual(
            RuleAction.Ignore, rule.ApplyAllowlistRule("aba", "FFF Troll",
                new FailurePart("Troll", FailureClassification.Person, 0)), "Rule SHOULD apply when FailureClassification matches As");

    }

    [Test]
    public void TestCombiningPatternAndPart()
    {
        var rule = new AllowlistRule()
        {
            IfPartPattern = "^Brian$",
            IfPattern = "^MR Brian And Skull$"
        };

        Assert.AreEqual(
            RuleAction.Ignore, rule.ApplyAllowlistRule("aba", "MR Brian And Skull",
                new FailurePart("Brian", FailureClassification.Person, 0)), "Rule matches on both patterns");

        Assert.AreEqual(
            RuleAction.None, rule.ApplyAllowlistRule("aba", "MR Brian And Skull",
                new FailurePart("Skull", FailureClassification.Person, 0)), "Rule does not match on both whole string AND part so should not be ignored");

        Assert.AreEqual(
            RuleAction.None, rule.ApplyAllowlistRule("aba", "MR Brian And Skull Dr Fisher",
                new FailurePart("Brian", FailureClassification.Person, 0)), "Rule does not match on both whole string AND part so should not be ignored");
    }


}
