using IsIdentifiable.Failures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace IsIdentifiable.Rules;

/// <summary>
/// Describes a service that can be consulted by IsIdentifiable to evaluate 
/// data and feed back whether it is identifiable.  This is done over TCP
/// socket and allows using IsIdentifiable with natural language processing
/// services written in other languages e.g. spaCy
/// </summary>
/// <remarks>
/// The protocol is as follows 
/// Send:
/// "StringToValidate\0"
/// Read:
/// "Classification\0Index0\0BadWord\0"  (these triplets are repeated when multiple failing parts are detected in a single string validated)
/// "Classification1\0Index1\0BadWord1\0Classification2\0Index2\0BadWord2\0"
///
/// Example:
/// "Person\000\0Dave\0" (the word Dave in the input string at index 0 is considered identifiable and is a 'Person')
/// </remarks>
public class SocketRule : IAppliableRule, IDisposable
{
    /// <summary>
    /// The name of the server that is running the listening service e.g. localhost
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// The port of the server that is running the listening service
    /// </summary>
    public int Port { get; set; }

    private TcpClient _tcp;
    private NetworkStream _stream;
    private System.IO.StreamWriter _write;
    private System.IO.StreamReader _read;

    /// <summary>
    /// Sends the <paramref name="fieldValue"/> to the service listening on <see cref="Host"/>.  The
    /// service is expected to reply indicating which parts of the field fail validation (are identifiable)
    /// </summary>
    /// <param name="fieldName">The column or tag name of the field being evaluated</param>
    /// <param name="fieldValue">A single cell or dicom tag that is to be evaluated for identifiable information by the remote service</param>
    /// <param name="badParts">When failing validation, this is a list of the sub words in the <paramref name="fieldValue"/> that
    /// are identifiable according to the remote classification service</param>
    /// <returns>Whether the full <paramref name="fieldValue"/> passed validation or should be reported</returns>
    /// <exception cref="Exception"></exception>
    public RuleAction Apply(string fieldName, string fieldValue, out IEnumerable<FailurePart> badParts)
    {
        if (_stream == null)
        {
            _tcp = new TcpClient(Host, Port);
            _stream = _tcp.GetStream();
            _write = new System.IO.StreamWriter(_stream);
            _read = new System.IO.StreamReader(_stream);
        }

        // Translate the passed message into ASCII and store it as a Byte array.

        _write.Write($"{fieldValue.Replace("\0", "")}\0");
        _write.Flush();

        var sb = new StringBuilder();

        int last, c = ' ';
        do
        {
            //if last character was a \0 and the next one we read is a \0 that marks the end of the response
            last = c;
            c = _read.Read();
            if (last <= '\0' && c <= '\0')
                break;
            if (sb.Length > 10000) throw new Exception($"Unexpected response {sb}");
            sb.Append((char)c);
        } while (true);


        badParts = HandleResponse(sb.ToString()).ToArray();

        return badParts.Any() ? RuleAction.Report : RuleAction.None;
    }

    /// <summary>
    /// Parses the socket <paramref name="responseData"/> recieved from the remote <see cref="Host"/>
    /// classification service into zero or more <see cref="FailurePart"/>
    /// </summary>
    /// <param name="responseData"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static IEnumerable<FailurePart> HandleResponse(string responseData)
    {
        var parts = 3;
        if (string.Equals(responseData, "\0") || string.IsNullOrWhiteSpace(responseData))
            yield break;

        if (responseData.Contains("\0\0"))
            throw new Exception("Invalid sequence detected: two null terminators in a row");

        var result = responseData.Split("\0", StringSplitOptions.RemoveEmptyEntries);

        if (result.Length % parts != 0)
            throw new Exception($"Expected tokens to arrive in multiples of {parts} (but got '{result.Length}').  Full message was '{responseData}' (expected <classification><offset> or <null terminator>)");

        for (var i = 0; i < result.Length; i += parts)
        {
            if (!Enum.TryParse<FailureClassification>(result[i], true, out var classification))
                throw new Exception($"Could not parse TCP client classification '{result[i]}' (expected a member of Enum FailureClassification)");

            if (!int.TryParse(result[i + 1], out var offset))
                throw new Exception($"Failed to parse offset from TCP client response.  Response was '{result[i + 1]}' (expected int)");

            var badWord = result[i + 2];

            yield return new FailurePart(badWord, classification, offset);
        }
    }

    /// <summary>
    /// Closes and disposes the TCP stream to <see cref="Host"/>
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _stream?.Dispose();
        _tcp?.Dispose();
    }
}
