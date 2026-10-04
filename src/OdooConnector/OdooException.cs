namespace OdooConnector;

/// <summary>
/// Base class for every error raised by the connector: the server could not be reached, answered with an
/// HTTP error or an invalid XML-RPC document, rejected the credentials (<see cref="OdooAuthenticationException"/>)
/// or reported a fault (<see cref="OdooFaultException"/>).
/// </summary>
public class OdooException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public OdooException()
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    public OdooException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    public OdooException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
