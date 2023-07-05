using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;

namespace IsIdentifiable.AllowLists;

/// <summary>
/// An AllowList source which returns the values in the first column of the provided Csv file.  The file must be properly escaped
/// if it has commas in fields etc.  There must be no header record.
/// </summary>
public class CsvAllowList : IAllowListSource
{
    private readonly StreamReader _streamReader;
    private readonly CsvReader _reader;
    private bool _firstTime = true;

    /// <summary>
    /// Reads all values in <paramref name="filePath"/>.  The contents of each line
    /// will be used as an 'ignore' value by IsIdentifiable (i.e. a list of false
    /// positives or values that match rules but should be ignored anyway).
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="fileSystem"></param>
    /// <exception cref="Exception"></exception>
    public CsvAllowList(string filePath, IFileSystem fileSystem)
    {
        if (!fileSystem.File.Exists(filePath))
            throw new Exception($"Could not find AllowList file at '{filePath}'");

        Stream stream = fileSystem.File.OpenRead(filePath);
        _streamReader = new StreamReader(stream);
        _reader = new CsvReader(_streamReader, new CsvConfiguration(System.Globalization.CultureInfo.CurrentCulture)
        {
            HasHeaderRecord = false
        });
    }

    /// <summary>
    /// Returns all 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string?> GetAllowList()
    {
        if (!_firstTime)
            throw new Exception("Allow list has already been read from file.  This method should only be called once");

        while (_reader.Read())
            yield return _reader[0];

        _firstTime = false;
    }

    /// <summary>
    /// Closes the file and disposes of IO handles and streams
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _reader.Dispose();
        _streamReader.Dispose();
    }
}
