namespace OdooConnector;

/// <summary>
/// Builds the commands Odoo uses to write one2many and many2many fields, mirroring Python's
/// <c>odoo.fields.Command</c>. For example, invoice lines are created with
/// <c>["invoice_line_ids"] = new[] { OdooCommand.Create(line1), OdooCommand.Create(line2) }</c>.
/// </summary>
public static class OdooCommand
{
    /// <summary><c>(0, 0, values)</c>: creates a new related record.</summary>
    public static object[] Create(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [0, 0, values];
    }

    /// <summary><c>(1, id, values)</c>: updates the related record <paramref name="id"/>.</summary>
    public static object[] Update(int id, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [1, id, values];
    }

    /// <summary><c>(2, id, 0)</c>: removes the related record from the relation and deletes it.</summary>
    public static object[] Delete(int id) => [2, id, 0];

    /// <summary><c>(3, id, 0)</c>: removes the related record from the relation without deleting it.</summary>
    public static object[] Unlink(int id) => [3, id, 0];

    /// <summary><c>(4, id, 0)</c>: adds an existing record to the relation.</summary>
    public static object[] Link(int id) => [4, id, 0];

    /// <summary><c>(5, 0, 0)</c>: removes every record from the relation.</summary>
    public static object[] Clear() => [5, 0, 0];

    /// <summary><c>(6, 0, ids)</c>: replaces the relation with exactly <paramref name="ids"/>.</summary>
    public static object[] Set(IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return [6, 0, ids.ToArray()];
    }
}
