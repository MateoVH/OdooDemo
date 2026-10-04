using System.Globalization;

namespace OdooConnector.Tests.Infrastructure;

/// <summary>
/// Renders decoded XML-RPC values in Python notation, so assertions read like the Odoo calls they check:
/// <c>[['&amp;', ['is_company', '=', True], ['country_id.code', '=', 'CO']]]</c>.
/// </summary>
internal static class Py
{
    public static string Repr(object? value) => value switch
    {
        null => "None",
        bool flag => flag ? "True" : "False",
        string text => $"'{text.Replace("\\", "\\\\").Replace("'", "\\'")}'",
        int or long => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        double number when number == Math.Floor(number) => number.ToString("0.0", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IReadOnlyDictionary<string, object?> members => $"{{{string.Join(", ", members.Select(member => $"{Repr(member.Key)}: {Repr(member.Value)}"))}}}",
        IEnumerable<object?> items => $"[{string.Join(", ", items.Select(Repr))}]",
        _ => throw new NotSupportedException($"Cannot render a {value.GetType().Name}."),
    };
}
