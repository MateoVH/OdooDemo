namespace OdooConnector;

/// <summary>
/// Odoo rejected the configured database, username or API key.
/// </summary>
public sealed class OdooAuthenticationException : OdooException
{
    /// <summary>Creates an exception with a default message.</summary>
    public OdooAuthenticationException()
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    public OdooAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    public OdooAuthenticationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
