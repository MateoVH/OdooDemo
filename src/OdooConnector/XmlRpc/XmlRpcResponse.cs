namespace OdooConnector.XmlRpc;

/// <summary>A <c>&lt;fault&gt;</c> returned by the server instead of a result.</summary>
internal sealed record XmlRpcFault(int Code, string Message);

/// <summary>The decoded content of a <c>methodResponse</c>: either a value or a fault.</summary>
internal readonly record struct XmlRpcResponse(object? Value, XmlRpcFault? Fault)
{
    public static XmlRpcResponse FromValue(object? value) => new(value, null);

    public static XmlRpcResponse FromFault(XmlRpcFault fault) => new(null, fault);
}
