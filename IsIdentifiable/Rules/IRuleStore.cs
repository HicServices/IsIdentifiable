namespace IsIdentifiable.Rules
{
    public interface IRuleStore
    {
        /// <summary>
        /// Add a rule to the store
        /// </summary>
        /// <param name="rule"></param>
        void Add(IRegexRule rule);

        /// <summary>
        /// Delete a rule from the store
        /// </summary>
        /// <param name="rule"></param>
        /// <returns>True if the rule existed and was deleted, false otherwise</returns>
        bool Delete(IRegexRule rule);

        /// <summary>
        /// Removes the most recently added entry from the store, if one exists
        /// </summary>
        void Undo();
    }
}
