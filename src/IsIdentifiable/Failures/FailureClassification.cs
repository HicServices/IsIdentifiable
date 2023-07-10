namespace IsIdentifiable.Failures;

/// <summary>
/// Describes the Type of text detected (person, date etc)
/// </summary>
public enum FailureClassification
{
    /// <summary>
    /// No classification
    /// </summary>
    None = 0,

    /// <summary>
    /// e.g. CHI number 
    /// </summary>
    PrivateIdentifier,

    /// <summary>
    /// A place or address
    /// </summary>
    Location,

    /// <summary>
    /// A persons name 
    /// </summary>
    Person,

    /// <summary>
    /// The name of an organization (hospital, company etc)
    /// </summary>
    Organization,

    /// <summary>
    /// A quantity of money
    /// </summary>
    Money,

    /// <summary>
    /// A percentage statistic
    /// </summary>
    Percent,

    /// <summary>
    /// A date.  This can be vague e.g. '1 Mar'
    /// </summary>
    Date,

    /// <summary>
    /// A time of day
    /// </summary>
    Time,

    /// <summary>
    /// Word(s) found in pixel data using OCR.
    /// </summary>
    PixelText,

    /// <summary>
    /// A postcode or zip code
    /// </summary>
    Postcode
}
