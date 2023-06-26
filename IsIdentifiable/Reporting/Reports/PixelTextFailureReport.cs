using System.Data;
using System.IO.Abstractions;
using IsIdentifiable.Failures;

namespace IsIdentifiable.Reporting.Reports;

internal class PixelTextFailureReport : FailureReport
{
    private readonly DataTable _dt = new();

    private readonly string[] _headerRow =
    {
        "Filename",
        "SOPInstanceUID",
        "PixelFormat",
        "ProcessedPixelFormat",
        "StudyInstanceUID",
        "SeriesInstanceUID",
        "Modality",
        "ImageType1",
        "ImageType2",
        "ImageType3",
        "MeanConfidence",
        "TextLength",
        "PixelText",
        "Rotation",
        "Frame",
        "Overlay"
    };

    public PixelTextFailureReport(string targetName, IFileSystem fileSystem)
        : base(targetName, fileSystem)
    {
        foreach (var s in _headerRow)
            _dt.Columns.Add(s);
    }

    public override void Add(Failure failure)
    {

    }

    protected override void CloseReportBase()
    {
        Destinations.ForEach(d => d.WriteItems(_dt));
    }

    //TODO Replace argument list with object
    public void FoundPixelData(IFileInfo fi, string sopID, string studyID, string seriesID, string modality, string[] imageType, float meanConfidence, int textLength, string pixelText, int rotation, int frame, int overlay)
    {
        var dr = _dt.Rows.Add();

        if (imageType != null && imageType.Length > 0)
            dr["ImageType1"] = imageType[0];
        if (imageType != null && imageType.Length > 1)
            dr["ImageType2"] = imageType[1];
        if (imageType != null && imageType.Length > 2)
            dr["ImageType3"] = imageType[2];

        //TODO Pull these out
        dr["Filename"] = fi.FullName;
        dr["SOPInstanceUID"] = sopID;

        dr["StudyInstanceUID"] = studyID;

        dr["SeriesInstanceUID"] = seriesID;
        dr["Modality"] = modality;

        dr["MeanConfidence"] = meanConfidence;
        dr["TextLength"] = textLength;
        dr["PixelText"] = pixelText;
        dr["Rotation"] = rotation;
        dr["Frame"] = frame;
        dr["Overlay"] = overlay;
    }
}