namespace OdooConnector.XmlRpc;

/// <summary>
/// Implemented by library types that know how to represent themselves as a plain XML-RPC value,
/// e.g. <see cref="OdooDomain"/> becomes an array of terms and <see cref="OdooReference"/> its id.
/// </summary>
internal interface IXmlRpcValue
{
    object? ToXmlRpcValue();
}
