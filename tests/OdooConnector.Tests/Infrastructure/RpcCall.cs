using System.Xml.Linq;
using OdooConnector.XmlRpc;

namespace OdooConnector.Tests.Infrastructure;

/// <summary>An XML-RPC request received by <see cref="FakeOdooServer"/>, decoded into plain values.</summary>
internal sealed class RpcCall
{
    private RpcCall(string path, string methodName, IReadOnlyList<object?> parameters)
    {
        Path = path;
        MethodName = methodName;
        Params = parameters;
    }

    /// <summary>Request path, e.g. <c>/xmlrpc/2/object</c>.</summary>
    public string Path { get; }

    /// <summary>Last segment of the path: <c>common</c> or <c>object</c>.</summary>
    public string Service => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>XML-RPC method: <c>version</c>, <c>authenticate</c> or <c>execute_kw</c>.</summary>
    public string MethodName { get; }

    public IReadOnlyList<object?> Params { get; }

    // execute_kw(db, uid, password, model, method, args, kwargs)
    public string Model => (string)Params[3]!;

    public string Method => (string)Params[4]!;

    public IReadOnlyList<object?> Args => (IReadOnlyList<object?>)Params[5]!;

    public IReadOnlyDictionary<string, object?> Kwargs => (IReadOnlyDictionary<string, object?>)Params[6]!;

    public static RpcCall Parse(Uri uri, string body)
    {
        var root = XDocument.Parse(body, LoadOptions.PreserveWhitespace).Root!;
        var parameters = root.Element("params")!
            .Elements("param")
            .Select(param => XmlRpcDeserializer.ParseValue(param.Element("value")!))
            .ToArray();

        return new RpcCall(uri.AbsolutePath, root.Element("methodName")!.Value, parameters);
    }

    public override string ToString() => $"{MethodName} {Py.Repr(Params)}";
}
