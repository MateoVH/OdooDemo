using System.Globalization;
using System.Resources;

namespace OdooConnector.Resources;

/// <summary>
/// Localized messages of the library, read from <c>Strings.resx</c> (English, the neutral language) and
/// <c>Strings.es.resx</c> (Spanish). The language follows <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
internal static class Strings
{
    internal static ResourceManager ResourceManager { get; } = new("OdooConnector.Resources.Strings", typeof(Strings).Assembly);

    // Options
    public static string UrlRequired => Get(nameof(UrlRequired));

    public static string UrlNotHttp(Uri url) => Format(nameof(UrlNotHttp), url);

    public static string UrlHasQuery(Uri url) => Format(nameof(UrlHasQuery), url);

    public static string DatabaseRequired => Get(nameof(DatabaseRequired));

    public static string UsernameRequired => Get(nameof(UsernameRequired));

    public static string ApiKeyRequired => Get(nameof(ApiKeyRequired));

    public static string InvalidOptions(string errors) => Format(nameof(InvalidOptions), errors);

    // Client
    public static string AuthenticationRejected(string username, string database) =>
        Format(nameof(AuthenticationRejected), username, database);

    public static string AccessDenied(string message) => Format(nameof(AccessDenied), message);

    public static string ServerUnreachable(Uri endpoint, string reason) => Format(nameof(ServerUnreachable), endpoint, reason);

    public static string RequestTimedOut(Uri endpoint) => Format(nameof(RequestTimedOut), endpoint);

    public static string InvalidResponse(Uri endpoint) => Format(nameof(InvalidResponse), endpoint);

    public static string HttpNotFound(Uri endpoint) => Format(nameof(HttpNotFound), endpoint);

    public static string HttpError(int statusCode, string status, Uri endpoint) => Format(nameof(HttpError), statusCode, status, endpoint);

    public static string UnexpectedResult(string model, string method, string type) => Format(nameof(UnexpectedResult), model, method, type);

    public static string EmptyFault => Get(nameof(EmptyFault));

    // Records and domains
    public static string FieldMissing(string field) => Format(nameof(FieldMissing), field);

    public static string FieldInvalidType(string field, string expected, string actual) =>
        Format(nameof(FieldInvalidType), field, expected, actual);

    public static string EmptyDomainNegation => Get(nameof(EmptyDomainNegation));

    // Invoices
    public static string InvoicePartnerRequired => Get(nameof(InvoicePartnerRequired));

    public static string InvoiceLinesRequired => Get(nameof(InvoiceLinesRequired));

    public static string InvoiceLineNull(int line) => Format(nameof(InvoiceLineNull), line);

    public static string InvoiceLineDescriptionRequired(int line) => Format(nameof(InvoiceLineDescriptionRequired), line);

    public static string InvoiceLineDiscountOutOfRange(int line) => Format(nameof(InvoiceLineDiscountOutOfRange), line);

    public static string UnknownInvoiceType => Get(nameof(UnknownInvoiceType));

    public static string NotAnInvoiceMoveType(string? moveType) => Format(nameof(NotAnInvoiceMoveType), moveType);

    // XML-RPC serialization
    public static string NestingTooDeep(int maxDepth) => Format(nameof(NestingTooDeep), maxDepth);

    public static string UnsupportedType(string type) => Format(nameof(UnsupportedType), type);

    public static string IntegerTooLarge => Get(nameof(IntegerTooLarge));

    public static string NonFiniteNumber(string value) => Format(nameof(NonFiniteNumber), value);

    public static string StructKeyNotString => Get(nameof(StructKeyNotString));

    // XML-RPC parsing
    public static string NotAMethodResponse => Get(nameof(NotAMethodResponse));

    public static string MissingResponseValue => Get(nameof(MissingResponseValue));

    public static string FaultWithoutValue => Get(nameof(FaultWithoutValue));

    public static string FaultNotStruct => Get(nameof(FaultNotStruct));

    public static string UnsupportedXmlRpcType(string type) => Format(nameof(UnsupportedXmlRpcType), type);

    public static string InvalidInt32(string text) => Format(nameof(InvalidInt32), text);

    public static string InvalidInt64(string text) => Format(nameof(InvalidInt64), text);

    public static string InvalidBoolean(string text) => Format(nameof(InvalidBoolean), text);

    public static string InvalidDouble(string text) => Format(nameof(InvalidDouble), text);

    public static string InvalidDateTime(string text) => Format(nameof(InvalidDateTime), text);

    public static string StructMemberWithoutName => Get(nameof(StructMemberWithoutName));

    public static string StructMemberWithoutValue(string name) => Format(nameof(StructMemberWithoutValue), name);

    private static string Get(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"The resource '{name}' is missing.");

    private static string Format(string name, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(name), args);
}
