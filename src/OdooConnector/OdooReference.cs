using OdooConnector.XmlRpc;

namespace OdooConnector;

/// <summary>
/// Value of a many2one field: the id of the related record and its display name,
/// e.g. <c>country_id = [49, "Colombia"]</c>.
/// </summary>
/// <remarks>When sent back to Odoo (in a domain or in the values of a create/write) it is sent as its id.</remarks>
/// <param name="Id">Id of the related record.</param>
/// <param name="Name">Display name of the related record.</param>
public sealed record OdooReference(int Id, string Name) : IXmlRpcValue
{
    object? IXmlRpcValue.ToXmlRpcValue() => Id;
}
