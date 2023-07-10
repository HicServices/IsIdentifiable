using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Tests;

class ConsensusRuleTests
{
    [Test]
    public void NoConsensus_OnlyOneHasProblem()
    {
        var rule = new ConsensusRule()
        {
            Rules = new IAppliableRule[]
            {
                new TestRule(RuleAction.Report,new FailurePart("bb",FailureClassification.Person)),
                new TestRule(RuleAction.Ignore),
            }
        };

        var result = rule.Apply("ff", "vv", out var badParts);

        Assert.AreEqual(RuleAction.None, result);
        Assert.IsEmpty(badParts);
    }

    [TestCase(-1)]
    [TestCase(10)]
    public void Consensus_Exact(int offset)
    {
        var rule = new ConsensusRule()
        {
            Rules = new IAppliableRule[]
            {
                new TestRule(RuleAction.Report,new FailurePart("bb",FailureClassification.Person,offset)),
                new TestRule(RuleAction.Report,new FailurePart("bb",FailureClassification.Person,offset)),
            }
        };

        var result = rule.Apply("ff", "vv", out var badParts);

        Assert.AreEqual(RuleAction.Report, result);
        Assert.AreEqual(offset, badParts.Single().Offset);
    }

    [Test]
    public void Consensus_SingleOverlap()
    {
        var rule = new ConsensusRule()
        {
            Rules = new IAppliableRule[]
            {
                // Word is length 2 and begins at offset 10
                new TestRule(RuleAction.Report,new FailurePart("ab",FailureClassification.Person,10)),
                new TestRule(RuleAction.Report,new FailurePart("bc",FailureClassification.Person,11)),
            }
        };

        var result = rule.Apply("ff", "abc is so cool", out var badParts);

        Assert.AreEqual(RuleAction.Report, result);
        var badPart = badParts.Single();
        Assert.AreEqual(10, badPart.Offset);
        Assert.AreEqual("ab", badPart.Word);
    }

    [Test]
    public void TestDeserialization()
    {
        var yaml =
            @"ConsensusRules:
    - Rules:
      - !SocketRule
          Host: 127.0.123.123
          Port: 1234
      - !SocketRule
          Host: 127.0.123.123
          Port: 567";


        var deserializer = RuleHelpers.GetRuleDeserializer();
        var ruleSet = deserializer.Deserialize<RuleSet>(yaml);

        Assert.IsInstanceOf(typeof(ConsensusRule), ruleSet.ConsensusRules.Single());
        Assert.IsInstanceOf(typeof(SocketRule), ruleSet.ConsensusRules.Single().Rules[0]);
        Assert.AreEqual(1234, ((SocketRule)ruleSet.ConsensusRules.Single().Rules[0]).Port);
        Assert.AreEqual(567, ((SocketRule)ruleSet.ConsensusRules.Single().Rules[1]).Port);
    }

    internal class TestRule : IAppliableRule
    {
        private readonly RuleAction _rule;
        private readonly FailurePart[] _parts;

        public TestRule(RuleAction rule, params FailurePart[] parts)
        {
            _parts = parts;
            _rule = rule;
        }

        public RuleAction Apply(string fieldName, string fieldValue, out IEnumerable<FailurePart> badParts)
        {
            badParts = _parts;

            return _rule;
        }
    }
}
