using OdooConnector.Resources;

namespace OdooConnector;

/// <summary>
/// Odoo processed the call but answered with an XML-RPC fault: a business rule was violated, the user lacks
/// permissions or the server raised an unexpected error.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> holds a one-line summary; for server errors Odoo sends the whole Python
/// traceback, which is kept in <see cref="FaultString"/>.
/// </remarks>
public sealed class OdooFaultException : OdooException
{
    /// <summary>Creates an exception from the fault code and fault string sent by Odoo.</summary>
    public OdooFaultException(int faultCode, string faultString)
        : base(Summarize(faultString))
    {
        FaultCode = faultCode;
        FaultString = faultString ?? string.Empty;
    }

    /// <summary>Creates an exception with a default message.</summary>
    public OdooFaultException()
        : this(0, string.Empty)
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    public OdooFaultException(string message)
        : this(0, message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    public OdooFaultException(string message, Exception? innerException)
        : base(Summarize(message), innerException)
    {
        FaultString = message ?? string.Empty;
    }

    /// <summary>Numeric fault code returned on <c>/xmlrpc/2/</c> (see <see cref="Kind"/>).</summary>
    public int FaultCode { get; }

    /// <summary>Full fault string sent by Odoo; for server errors it contains the Python traceback.</summary>
    public string FaultString { get; } = string.Empty;

    /// <summary>Category of the fault, derived from <see cref="FaultCode"/>.</summary>
    public OdooFaultKind Kind =>
        Enum.IsDefined((OdooFaultKind)FaultCode) ? (OdooFaultKind)FaultCode : OdooFaultKind.Unknown;

    private static string Summarize(string? faultString)
    {
        var text = faultString?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Strings.EmptyFault;
        }

        if (!text.StartsWith("Traceback", StringComparison.Ordinal))
        {
            return text;
        }

        // The last line of a Python traceback is the actual error, e.g. "ValueError: Invalid field 'foo'".
        return text.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.Length > 0) ?? text;
    }
}
