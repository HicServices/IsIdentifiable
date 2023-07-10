using System.Text.RegularExpressions;

namespace IsIdentifiable.Util;

internal static class RegexConstants
{
    /// <summary>
    /// Matches Scotland's Community Health Index (CHI) numbers
    /// DDMMYY + 4 digits. \b bounded i.e. not more than 10 digits
    /// </summary>
    public static readonly Regex ChiRegex = new(@"\b[0-3][0-9][0-1][0-9][0-9]{6}\b");

    /// <summary>
    /// Matches UK postcodes
    /// </summary>
    public static readonly Regex PostcodeRegex = new(@"\b((GIR 0AA)|((([A-Z-[QVX]][0-9][0-9]?)|(([A-Z-[QVX]][A-Z-[IJZ]][0-9][0-9]?)|(([A-Z-[QVX]][0-9][A-HJKSTUW])|([A-Z-[QVX]][A-Z-[IJZ]][0-9][ABEHMNPRVWXY]))))\s?[0-9][A-Z-[CIKMOV]]{2}))\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches a 'symbol' (digit followed by an optional th, rd or separator) then a month name (e.g. Jan or January)
    /// </summary>
    public static readonly Regex SymbolThenMonth = new(@"\d+((th)|(rd)|(st)|[\-/\\])?\s?((Jan(uary)?)|(Feb(ruary)?)|(Mar(ch)?)|(Apr(il)?)|(May)|(June?)|(July?)|(Aug(ust)?)|(Sep(tember)?)|(Oct(ober)?)|(Nov(ember)?)|(Dec(ember)?))", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches a month name (e.g. Jan or January) followed by a 'symbol' (digit followed by an optional th, rd or separator) then a
    /// </summary>
    public static readonly Regex MonthThenSymbol = new(@"((Jan(uary)?)|(Feb(ruary)?)|(Mar(ch)?)|(Apr(il)?)|(May)|(June?)|(July?)|(Aug(ust)?)|(Sep(tember)?)|(Oct(ober)?)|(Nov(ember)?)|(Dec(ember)?))[\s\-/\\]?\d+((th)|(rd)|(st))?", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches digits followed by a separator (: - \ etc) followed by more digits with optional AM / PM / GMT at the end
    /// However this looks more like a time than a date and I would argue that times are not PII?
    /// It's also not restrictive enough so matches too many non-PII numerics.
    /// </summary>
    public static readonly Regex Date = new(@"\b\d+([:\-/\\]\d+)+\s?((AM)|(PM)|(GMT))?\b", RegexOptions.IgnoreCase);

    // The following regex were adapted from:
    // https://www.oreilly.com/library/view/regular-expressions-cookbook/9781449327453/ch04s04.html
    // Separators are space slash dash
    #region

    /// <summary>
    /// Matches year last, i.e d/m/y or m/d/y
    /// </summary>
    public static readonly Regex DateYearLast = new(@"\b(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))[ ]?[/-][ ]?(?:[0-9]{2})?[0-9]{2}(\b|T)");
    /// <summary>
    /// Matches year first, i.e y/m/d or y/d/m
    /// </summary>
    public static readonly Regex DateYearFirst = new(@"\b(?:[0-9]{2})?[0-9]{2}[ ]?[/-][ ]?(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))(\b|T)");

    /// <summary>
    /// Matches year missing, i.e d/m or m/d
    /// </summary>
    public static readonly Regex DateYearMissing = new(@"\b(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))(\b|T)");

    #endregion
}
