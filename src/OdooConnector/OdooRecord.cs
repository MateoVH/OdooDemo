using System.Globalization;
using OdooConnector.Resources;

namespace OdooConnector;

/// <summary>
/// One record returned by <c>read</c> or <c>search_read</c>, with typed accessors that understand Odoo's
/// conventions: empty fields come back as <c>false</c>, many2one fields as <c>[id, "name"]</c> pairs,
/// x2many fields as lists of ids and dates as <c>yyyy-MM-dd</c> strings.
/// </summary>
public sealed class OdooRecord
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"];
    private static readonly string[] DateTimeFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFF", "yyyy-MM-dd"];

    /// <summary>Wraps the raw field values of a record.</summary>
    public OdooRecord(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = fields;
    }

    /// <summary>Raw field values, exactly as decoded from XML-RPC.</summary>
    public IReadOnlyDictionary<string, object?> Fields { get; }

    /// <summary>The record id (the <c>id</c> field).</summary>
    public int Id => GetInt32("id");

    /// <summary>Raw value of a field, exactly as decoded from XML-RPC.</summary>
    /// <exception cref="KeyNotFoundException">The field was not returned by Odoo.</exception>
    public object? this[string field] => GetRaw(field);

    /// <summary>Whether the field has a value (Odoo sends <c>false</c> for empty fields).</summary>
    public bool HasValue(string field) => GetRaw(field) is not (null or false);

    /// <summary>Reads a char, text, html or selection field; <see langword="null"/> when empty.</summary>
    public string? GetString(string field) => GetRaw(field) switch
    {
        null or false => null,
        string text => text,
        var other => throw InvalidType(field, other, "char"),
    };

    /// <summary>Reads a boolean field.</summary>
    public bool GetBoolean(string field) => GetRaw(field) switch
    {
        null => false,
        bool flag => flag,
        var other => throw InvalidType(field, other, "boolean"),
    };

    /// <summary>Reads an integer field; 0 when empty.</summary>
    public int GetInt32(string field) => GetRaw(field) switch
    {
        null or false => 0,
        int number => number,
        long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
        var other => throw InvalidType(field, other, "integer"),
    };

    /// <summary>Reads a float or monetary field as <see cref="decimal"/>; 0 when empty.</summary>
    public decimal GetDecimal(string field) => GetRaw(field) switch
    {
        null or false => 0m,
        // Rounds to 15 significant digits, so 99.99 (double) becomes exactly 99.99m.
        double number => (decimal)number,
        int number => number,
        long number => number,
        var other => throw InvalidType(field, other, "float"),
    };

    /// <summary>Reads a float or monetary field as <see cref="double"/>; 0 when empty.</summary>
    public double GetDouble(string field) => GetRaw(field) switch
    {
        null or false => 0d,
        double number => number,
        int number => number,
        long number => number,
        var other => throw InvalidType(field, other, "float"),
    };

    /// <summary>Reads a date field (<c>yyyy-MM-dd</c>); <see langword="null"/> when empty.</summary>
    public DateOnly? GetDate(string field) => GetRaw(field) switch
    {
        null or false => null,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text when DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            => DateOnly.FromDateTime(date),
        var other => throw InvalidType(field, other, "date"),
    };

    /// <summary>
    /// Reads a datetime field (<c>yyyy-MM-dd HH:mm:ss</c>). Odoo stores datetimes in UTC, so the result has
    /// <see cref="DateTimeKind.Utc"/>. Returns <see langword="null"/> when empty.
    /// </summary>
    public DateTime? GetDateTime(string field) => GetRaw(field) switch
    {
        null or false => null,
        DateTime dateTime => dateTime,
        string text when DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime)
            => dateTime,
        var other => throw InvalidType(field, other, "datetime"),
    };

    /// <summary>Reads a many2one field (<c>[id, "name"]</c>); <see langword="null"/> when empty.</summary>
    public OdooReference? GetReference(string field) => GetRaw(field) switch
    {
        null or false => null,
        IReadOnlyList<object?> { Count: 2 } pair when pair[0] is int id => new OdooReference(id, pair[1] as string ?? string.Empty),
        int id => new OdooReference(id, string.Empty),
        var other => throw InvalidType(field, other, "many2one"),
    };

    /// <summary>Reads a one2many or many2many field (a list of ids); empty when there are none.</summary>
    public IReadOnlyList<int> GetIds(string field) => GetRaw(field) switch
    {
        null or false => [],
        IReadOnlyList<object?> items => items.Select(item => item as int? ?? throw InvalidType(field, item, "one2many/many2many")).ToArray(),
        var other => throw InvalidType(field, other, "one2many/many2many"),
    };

    private object? GetRaw(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return Fields.TryGetValue(field, out var value) ? value : throw new KeyNotFoundException(Strings.FieldMissing(field));
    }

    // The message names the Odoo field type and the .NET type received, never the value (it may be personal data).
    private static InvalidCastException InvalidType(string field, object? value, string expected) =>
        new(Strings.FieldInvalidType(field, expected, value?.GetType().Name ?? "null"));
}
