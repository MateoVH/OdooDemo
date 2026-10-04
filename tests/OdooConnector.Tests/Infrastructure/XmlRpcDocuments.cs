using System.Text;
using System.Xml;
using OdooConnector.XmlRpc;

namespace OdooConnector.Tests.Infrastructure;

/// <summary>Builds XML-RPC <c>methodResponse</c> documents for the fake server.</summary>
internal static class XmlRpcDocuments
{
    public static string Response(object? value) => Write(writer =>
    {
        writer.WriteStartElement("methodResponse");
        writer.WriteStartElement("params");
        writer.WriteStartElement("param");
        XmlRpcSerializer.WriteValue(writer, value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    public static string Fault(int code, string message) => Write(writer =>
    {
        writer.WriteStartElement("methodResponse");
        writer.WriteStartElement("fault");
        XmlRpcSerializer.WriteValue(writer, new Dictionary<string, object?> { ["faultCode"] = code, ["faultString"] = message });
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static string Write(Action<XmlWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            writer.WriteStartDocument();
            write(writer);
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
