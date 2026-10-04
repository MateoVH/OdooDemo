using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using OdooConnector.Resources;

namespace OdooConnector.XmlRpc;

/// <summary>
/// Parses XML-RPC <c>methodResponse</c> documents into plain CLR values.
/// </summary>
/// <remarks>
/// Value mapping: <c>int</c>/<c>i4</c> → <see cref="int"/>, <c>i8</c> → <see cref="long"/>,
/// <c>boolean</c> → <see cref="bool"/>, <c>double</c> → <see cref="double"/>, <c>string</c> and untyped values →
/// <see cref="string"/>, <c>dateTime.iso8601</c> → <see cref="DateTime"/>, <c>base64</c> → <see cref="byte"/>[],
/// <c>nil</c> → <see langword="null"/>, <c>array</c> → <see cref="object"/>[] and
/// <c>struct</c> → <see cref="Dictionary{TKey,TValue}"/> with string keys.
/// </remarks>
internal static class XmlRpcDeserializer
{
    private static readonly XmlReaderSettings AsyncSettings = CreateSettings(async: true);
    private static readonly XmlReaderSettings SyncSettings = CreateSettings(async: false);

    private static readonly string[] DateTimeFormats =
    [
        "yyyyMMdd'T'HH:mm:ss",
        "yyyyMMdd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    public static async Task<XmlRpcResponse> ReadMethodResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(stream, AsyncSettings);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        return ParseMethodResponse(document);
    }

    public static XmlRpcResponse ParseMethodResponse(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), SyncSettings);
        return ParseMethodResponse(XDocument.Load(reader));
    }

    public static object? ParseValue(XElement value)
    {
        var typed = value.Elements().FirstOrDefault();
        if (typed is null)
        {
            // A <value> without a type element is a string, whitespace included.
            return value.Value;
        }

        var text = typed.Value;
        return typed.Name.LocalName switch
        {
            "string" => text,
            "int" or "i4" => ParseInt32(text),
            "i8" => ParseInt64(text),
            "boolean" => ParseBoolean(text),
            "double" => ParseDouble(text),
            "dateTime.iso8601" => ParseDateTime(text),
            "base64" => Convert.FromBase64String(text),
            "nil" => null,
            "struct" => ParseStruct(typed),
            "array" => ParseArray(typed),
            var other => throw new FormatException(Strings.UnsupportedXmlRpcType(other)),
        };
    }

    private static XmlReaderSettings CreateSettings(bool async) => new()
    {
        Async = async,
        // Odoo never sends DTDs; refusing them blocks XXE and entity-expansion ("billion laughs") attacks.
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    private static XmlRpcResponse ParseMethodResponse(XDocument document)
    {
        var root = document.Root;
        if (root?.Name.LocalName != "methodResponse")
        {
            throw new FormatException(Strings.NotAMethodResponse);
        }

        if (root.Element("fault") is { } fault)
        {
            return XmlRpcResponse.FromFault(ParseFault(fault));
        }

        var value = root.Element("params")?.Element("param")?.Element("value")
            ?? throw new FormatException(Strings.MissingResponseValue);

        return XmlRpcResponse.FromValue(ParseValue(value));
    }

    private static XmlRpcFault ParseFault(XElement fault)
    {
        var value = fault.Element("value") ?? throw new FormatException(Strings.FaultWithoutValue);
        if (ParseValue(value) is not Dictionary<string, object?> members)
        {
            throw new FormatException(Strings.FaultNotStruct);
        }

        var code = members.GetValueOrDefault("faultCode") switch
        {
            int number => number,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => 0,
        };

        return new XmlRpcFault(code, members.GetValueOrDefault("faultString") as string ?? string.Empty);
    }

    private static int ParseInt32(string text) =>
        int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException(Strings.InvalidInt32(text));

    private static long ParseInt64(string text) =>
        long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException(Strings.InvalidInt64(text));

    private static bool ParseBoolean(string text) => text.Trim() switch
    {
        "1" or "true" => true,
        "0" or "false" => false,
        _ => throw new FormatException(Strings.InvalidBoolean(text)),
    };

    private static double ParseDouble(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException(Strings.InvalidDouble(text));

    private static DateTime ParseDateTime(string text) =>
        DateTime.TryParseExact(text.Trim(), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : throw new FormatException(Strings.InvalidDateTime(text));

    private static Dictionary<string, object?> ParseStruct(XElement element)
    {
        var members = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in element.Elements("member"))
        {
            var name = member.Element("name")?.Value ?? throw new FormatException(Strings.StructMemberWithoutName);
            var value = member.Element("value") ?? throw new FormatException(Strings.StructMemberWithoutValue(name));

            members[name] = ParseValue(value);
        }

        return members;
    }

    private static object?[] ParseArray(XElement element) =>
        element.Element("data")?.Elements("value").Select(ParseValue).ToArray() ?? [];
}
