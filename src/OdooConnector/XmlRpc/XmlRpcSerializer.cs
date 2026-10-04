using System.Collections;
using System.Globalization;
using System.Text;
using System.Xml;
using OdooConnector.Resources;

namespace OdooConnector.XmlRpc;

/// <summary>
/// Writes XML-RPC <c>methodCall</c> documents from plain CLR values.
/// </summary>
/// <remarks>
/// Supported values: <see langword="null"/> (<c>&lt;nil/&gt;</c>), strings, booleans, integers, floating point
/// numbers, decimals, dates, byte arrays (<c>base64</c>), dictionaries with string keys (<c>struct</c>) and any
/// other <see cref="IEnumerable"/> (<c>array</c>).
/// Dates are sent exactly as Odoo sends them back: <see cref="DateOnly"/> as <c>yyyy-MM-dd</c> and
/// <see cref="DateTime"/>/<see cref="DateTimeOffset"/> as UTC <c>yyyy-MM-dd HH:mm:ss</c> strings, which is the
/// format the Odoo ORM expects in field values and domains.
/// </remarks>
internal static class XmlRpcSerializer
{
    private const int MaxDepth = 64;

    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        // Keeps "\r" as &#xD; so strings survive the line-ending normalization of XML parsers.
        NewLineHandling = NewLineHandling.Entitize,
    };

    public static byte[] SerializeMethodCall(string methodName, IReadOnlyList<object?> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameters);

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, Settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("methodCall");
            writer.WriteElementString("methodName", methodName);
            writer.WriteStartElement("params");
            foreach (var parameter in parameters)
            {
                writer.WriteStartElement("param");
                WriteValue(writer, parameter, depth: 0);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a single <c>&lt;value&gt;</c> element.</summary>
    public static void WriteValue(XmlWriter writer, object? value) => WriteValue(writer, value, depth: 0);

    private static void WriteValue(XmlWriter writer, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException(Strings.NestingTooDeep(MaxDepth));
        }

        if (value is IXmlRpcValue convertible)
        {
            value = convertible.ToXmlRpcValue();
        }

        writer.WriteStartElement("value");
        switch (value)
        {
            case null:
                writer.WriteStartElement("nil");
                writer.WriteEndElement();
                break;
            case string text:
                writer.WriteElementString("string", text);
                break;
            case bool boolean:
                writer.WriteElementString("boolean", boolean ? "1" : "0");
                break;
            case int or short or ushort or byte or sbyte:
                writer.WriteElementString("int", Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case long or uint or ulong:
                WriteInteger(writer, value);
                break;
            case double number:
                WriteDouble(writer, number, number.ToString("R", CultureInfo.InvariantCulture));
                break;
            case float number:
                // Formatting the float itself keeps 0.1f as "0.1" instead of 0.10000000149011612.
                WriteDouble(writer, number, number.ToString("R", CultureInfo.InvariantCulture));
                break;
            case decimal number:
                writer.WriteElementString("double", number.ToString(CultureInfo.InvariantCulture));
                break;
            case DateOnly date:
                writer.WriteElementString("string", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case DateTime dateTime:
                writer.WriteElementString("string", FormatDateTime(dateTime));
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteElementString("string", FormatDateTime(dateTimeOffset.UtcDateTime));
                break;
            case byte[] bytes:
                writer.WriteElementString("base64", Convert.ToBase64String(bytes));
                break;
            case IEnumerable<KeyValuePair<string, object?>> members:
                WriteStruct(writer, members.Select(member => (member.Key, member.Value)), depth);
                break;
            case IDictionary dictionary:
                WriteStruct(writer, ReadEntries(dictionary), depth);
                break;
            case IEnumerable items:
                WriteArray(writer, items, depth);
                break;
            default:
                throw new NotSupportedException(Strings.UnsupportedType(value.GetType().FullName ?? value.GetType().Name));
        }

        writer.WriteEndElement();
    }

    private static void WriteInteger(XmlWriter writer, object value)
    {
        var number = value switch
        {
            long signed => signed,
            uint unsigned => unsigned,
            ulong unsigned when unsigned <= long.MaxValue => (long)unsigned,
            _ => throw new NotSupportedException(Strings.IntegerTooLarge),
        };

        // <i8> is an extension (understood by Python's xmlrpc, and therefore by Odoo); use it only when needed.
        var tag = number is >= int.MinValue and <= int.MaxValue ? "int" : "i8";
        writer.WriteElementString(tag, number.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteDouble(XmlWriter writer, double number, string text)
    {
        if (!double.IsFinite(number))
        {
            throw new NotSupportedException(Strings.NonFiniteNumber(text));
        }

        writer.WriteElementString("double", text);
    }

    private static string FormatDateTime(DateTime value)
    {
        // Odoo stores datetimes as naive UTC values.
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static IEnumerable<(string Name, object? Value)> ReadEntries(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string name)
            {
                throw new NotSupportedException(Strings.StructKeyNotString);
            }

            yield return (name, entry.Value);
        }
    }

    private static void WriteStruct(XmlWriter writer, IEnumerable<(string Name, object? Value)> members, int depth)
    {
        writer.WriteStartElement("struct");
        foreach (var (name, value) in members)
        {
            writer.WriteStartElement("member");
            writer.WriteElementString("name", name);
            WriteValue(writer, value, depth + 1);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteArray(XmlWriter writer, IEnumerable items, int depth)
    {
        writer.WriteStartElement("array");
        writer.WriteStartElement("data");
        foreach (var item in items)
        {
            WriteValue(writer, item, depth + 1);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }
}
