namespace IsIdentifiable.Rules;

/// <summary>
/// Determines which bits of a failure get converted to corresponding symbols
/// </summary>
public enum SymbolsRuleFactoryMode
{
    /// <summary>
    /// Generates rules that match characters [A-Z]/[a-z] (depending on capitalization of input string) and digits \d
    /// </summary>
    Full,

    /// <summary>
    /// Generates rules that match any digits using \d
    /// </summary>
    DigitsOnly,

    /// <summary>
    /// Generates rules that match any characters with [A-Z]/[a-z] (depending on capitalization of input string)
    /// </summary>
    CharactersOnly,
}
