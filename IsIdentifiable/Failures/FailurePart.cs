using Equ;

namespace IsIdentifiable.Failures;

/// <summary>
/// Part of a <see cref="Failure"/>.  Describes a section (e.g. a word) of the full cell that is marked as IsIdentifiable.
/// A <see cref="Failure"/> can have multiple <see cref="FailurePart"/> e.g. if it is free text with multiple failing
/// words.
/// </summary>
public class FailurePart : MemberwiseEquatable<FailurePart>
{
    /// <summary>
    /// The classification of the failure e.g. CHI, PERSON, TextInPixel
    /// </summary>
    public FailureClassification Classification { get; set; }

    /// <summary>
    /// The location in a string in which the ProblemValue appeared.
    /// 
    /// <para>-1 if not appropriate (e.g. text detected in an image)</para>
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// The word that failed validation
    /// </summary>
    public string Word { get; set; }

    /// <summary>
    /// Creates a new instance that describes that a part of a cell failed validation and
    /// is considered identifiable.  The failing part is <paramref name="word"/> (although
    /// if a detector matches multiple words it can be more than one.  
    /// </summary>
    /// <param name="word">Part of <see cref="Failure.ProblemValue"/> that is identifiable</param>
    /// <param name="classification">The type of identifying information detected e.g. name, date</param>
    /// <param name="offset">index into the parent <see cref="Failure.ProblemValue"/> that the <paramref name="word"/> starts at</param>
    public FailurePart(string word,FailureClassification classification,int offset =-1)
    {
        Word = word;
        Classification = classification;
        Offset = offset;
    }

    /// <summary>
    /// Returns true if the provided <paramref name="index"/> is within the problem part of the original string
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool Includes(int index)
    {
        if (Offset == -1)
            return false;

        if (string.IsNullOrWhiteSpace(Word))
            return false;

        return index >= Offset && index < Offset + Word.Length;
    }
        
    /// <summary>
    /// Returns true if the failure part includes ANY of the indexes between start and start+length
    /// </summary>
    /// <param name="start"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public bool Includes(int start, int length)
    {
        for (var i = start; i < start + length; i++)
            if(Includes(i))
                return true;

        return false;
    }
}
