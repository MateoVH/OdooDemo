namespace OdooConnector;

/// <summary>
/// Categories of the fault codes Odoo returns on its <c>/xmlrpc/2/</c> endpoints.
/// </summary>
public enum OdooFaultKind
{
    /// <summary>The fault code is not one of the codes documented by Odoo.</summary>
    Unknown = 0,

    /// <summary>Unexpected server error (a Python exception); the fault string contains the traceback.</summary>
    ServerError = 1,

    /// <summary>
    /// A business rule was violated: <c>UserError</c>, <c>ValidationError</c>, <c>MissingError</c>...
    /// The message is meant to be shown to users.
    /// </summary>
    UserError = 2,

    /// <summary>The credentials were rejected (<c>AccessDenied</c>).</summary>
    AccessDenied = 3,

    /// <summary>The user is authenticated but not allowed to perform the operation (<c>AccessError</c>).</summary>
    AccessError = 4,
}
