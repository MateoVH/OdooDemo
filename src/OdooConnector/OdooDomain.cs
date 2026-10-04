using System.Collections;
using System.Globalization;
using OdooConnector.Resources;
using OdooConnector.XmlRpc;

namespace OdooConnector;

/// <summary>
/// Immutable builder for Odoo search domains, the filters used by <c>search</c>, <c>search_read</c> and
/// <c>search_count</c>.
/// </summary>
/// <remarks>
/// <para>
/// Conditions are written as in Python — <c>OdooDomain.Where("is_company", "=", true)</c> is
/// <c>[('is_company', '=', True)]</c> — and combined with <see cref="And(OdooDomain)"/>, <see cref="Or(OdooDomain)"/>
/// and <see cref="Not"/>. The result is sent in the prefix (Polish) notation Odoo uses, so
/// <c>(A and B) or C</c> becomes <c>['|', '&amp;', A, B, C]</c>.
/// </para>
/// <para>
/// <see cref="Empty"/> matches every record and is ignored when combined, like Odoo's own
/// <c>expression.AND</c>/<c>expression.OR</c> helpers. <see langword="null"/> values are sent as <c>False</c>,
/// which is how Odoo represents "not set".
/// </para>
/// </remarks>
public sealed class OdooDomain : IXmlRpcValue
{
    private const string AndOperator = "&";
    private const string OrOperator = "|";
    private const string NotOperator = "!";

    // Single expression in prefix notation: operators are strings, conditions are object?[3] arrays.
    private readonly object[] _terms;

    private OdooDomain(object[] terms) => _terms = terms;

    /// <summary>A domain without conditions: it matches every record.</summary>
    public static OdooDomain Empty { get; } = new([]);

    /// <summary>Whether this domain has no conditions.</summary>
    public bool IsEmpty => _terms.Length == 0;

    /// <summary>Creates a domain with a single condition, e.g. <c>Where("email", "ilike", "@acme.com")</c>.</summary>
    /// <param name="field">Field name; dotted paths such as <c>country_id.code</c> are allowed.</param>
    /// <param name="operator">Odoo operator: <c>=</c>, <c>!=</c>, <c>&gt;</c>, <c>ilike</c>, <c>in</c>, <c>child_of</c>...</param>
    /// <param name="value">Value to compare with; collections are sent as lists (for <c>in</c>/<c>not in</c>).</param>
    public static OdooDomain Where(string field, string @operator, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(@operator);

        return new([new[] { field, @operator, value ?? false }]);
    }

    /// <summary>Combines domains with AND, ignoring empty ones.</summary>
    public static OdooDomain AllOf(params OdooDomain[] domains) =>
        (domains ?? throw new ArgumentNullException(nameof(domains))).Aggregate(Empty, (result, domain) => result.And(domain));

    /// <summary>Combines domains with OR, ignoring empty ones.</summary>
    public static OdooDomain AnyOf(params OdooDomain[] domains) =>
        (domains ?? throw new ArgumentNullException(nameof(domains))).Aggregate(Empty, (result, domain) => result.Or(domain));

    /// <summary>Negates a domain.</summary>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty.</exception>
    public static OdooDomain Not(OdooDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (domain.IsEmpty)
        {
            throw new ArgumentException(Strings.EmptyDomainNegation, nameof(domain));
        }

        return new([NotOperator, .. domain._terms]);
    }

    /// <summary>Returns a domain that matches records matching both this domain and <paramref name="other"/>.</summary>
    public OdooDomain And(OdooDomain other) => Combine(AndOperator, this, other);

    /// <summary>Adds a condition with AND.</summary>
    public OdooDomain And(string field, string @operator, object? value) => And(Where(field, @operator, value));

    /// <summary>Returns a domain that matches records matching this domain or <paramref name="other"/>.</summary>
    public OdooDomain Or(OdooDomain other) => Combine(OrOperator, this, other);

    /// <summary>Adds a condition with OR.</summary>
    public OdooDomain Or(string field, string @operator, object? value) => Or(Where(field, @operator, value));

    /// <summary>Python representation of the domain, handy for logs and debugging.</summary>
    public override string ToString() => $"[{string.Join(", ", _terms.Select(FormatTerm))}]";

    object? IXmlRpcValue.ToXmlRpcValue() => _terms;

    private static OdooDomain Combine(string @operator, OdooDomain left, OdooDomain right)
    {
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsEmpty)
        {
            return right;
        }

        return right.IsEmpty ? left : new([@operator, .. left._terms, .. right._terms]);
    }

    private static string FormatTerm(object term) => term switch
    {
        object?[] condition => $"({FormatValue(condition[0])}, {FormatValue(condition[1])}, {FormatValue(condition[2])})",
        _ => FormatValue(term),
    };

    private static string FormatValue(object? value) => value switch
    {
        null => "None",
        bool flag => flag ? "True" : "False",
        string text => $"'{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'",
        DateOnly date => $"'{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'",
        DateTime dateTime => $"'{dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'",
        OdooDomain domain => domain.ToString(),
        OdooReference reference => reference.Id.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable items => $"[{string.Join(", ", items.Cast<object?>().Select(FormatValue))}]",
        _ => value.ToString() ?? string.Empty,
    };
}
