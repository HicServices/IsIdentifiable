using IsIdentifiable.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;

namespace IsIdentifiable.Rules.Storage
{
    public class YamlRegexRuleStore : RegexRuleStore, IDisposable
    {
        private readonly IFileInfo _rulesFile;
        private StreamWriter _streamWriter = null;

        private readonly DateTimeProvider _dateTimeProvider;

        private readonly Stack<string> _serializedRuleHistory = new();

        public YamlRegexRuleStore(
            IRegexRuleGenerator ruleGenerator,
            IFileInfo rulesFile,
            DateTimeProvider dateTimeProvider
        )
            : base(ruleGenerator)
        {
            _rulesFile = rulesFile;
            _dateTimeProvider = dateTimeProvider;

            var rules = RuleHelpers.LoadFrom(rulesFile, createIfMissing: true);
            SetInitialRules(rules);
        }

        protected override void AddImpl(IRegexRule rule)
        {
            var ruleYaml = RuleHelpers.SerializeWithComment(rule, _dateTimeProvider.UtcNow());

            _streamWriter ??= _rulesFile.AppendText();
            _streamWriter.Write(ruleYaml);

            _serializedRuleHistory.Push(ruleYaml);
        }

        protected override void RemoveImpl(IRegexRule rule)
        {
            var ruleYaml = RuleHelpers.Serialize(rule);
            Remove(ruleYaml);
        }

        protected override void UndoImpl(IRegexRule _)
        {
            var ruleYaml = _serializedRuleHistory.Pop();
            Remove(ruleYaml);
        }

        private void Remove(string ruleYaml)
        {
            var comment = $"# Rule deleted by {Environment.UserName} - {_dateTimeProvider.UtcNow()}{Environment.NewLine}";

            Flush();

            var oldText = _rulesFile.FileSystem.File.ReadAllText(_rulesFile.FullName);
            var newText = oldText.Replace(ruleYaml, comment);

            if (newText.Equals(oldText))
                return;

            var stream = _rulesFile.Open(FileMode.Truncate);
            _streamWriter = new StreamWriter(stream);
            _streamWriter.Write(newText);
        }

        /// <summary>
        /// Flush any pending writes to the rules file
        /// </summary>
        public void Flush() => _streamWriter?.Flush();

        /// <inheritdoc/>
        protected override void ClearImpl()
        {
            var stream = _rulesFile.Open(FileMode.Truncate);
            _streamWriter = new StreamWriter(stream);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _streamWriter?.Dispose();

            GC.SuppressFinalize(this);
        }

    }
}
