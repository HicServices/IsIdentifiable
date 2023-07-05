namespace IsIdentifiable.Options;

public sealed class DicomFileScannerOptions : ScannerBaseOptions, IFileScannerOptions
{
    /// <inheritdoc/>
    public string SearchPattern { get; init; } = DEFAULT_DCM_PREFIX;

    /// <summary>
    /// If non-zero, will ignore any reported pixel data text less than (but not equal to) the specified number of characters
    /// </summary>
    public int IgnoreTextLessThan { get; init; } = 0;

    /// <summary>
    /// If set any image tag which contains a DateTime will result in a failure
    /// </summary>
    public bool DatesAreFailures { get; init; } = false;

    /// <summary>
    /// If <see cref="DatesAreFailures"/> is set then this value will not result in a failure e.g., 0001-01-01. Otherwise this is ignored
    /// </summary>
    public string? ExcludedDate { get; init; }

    /// <summary>
    /// If set, images will be rotated to 90, 180 and 270 degrees (clockwise) to allow OCR to pick up upside down or horizontal text. Only applies if OCR is running
    /// </summary>
    public bool Rotate { get; init; } = true;

    /// <summary>
    /// If specified then the DICOM file's pixel data will be run through text detection. <see cref="TessDirectory"/> must be set
    /// </summary>
    public bool RunOCR { get; init; } = false;

    /// <summary>
    /// Path to a directory where tessdata.eng must exist. 
    /// </summary>
    public string? TessDirectory { get; init; }

    /// <inheritdoc/>
    public bool StopOnError { get; init; } = false;


    private const string DEFAULT_DCM_PREFIX = "*.dcm";
}
