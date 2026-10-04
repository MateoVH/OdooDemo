namespace OdooConnector.Contacts;

/// <summary>A contact (<c>res.partner</c>): a company or an individual.</summary>
public sealed record Contact
{
    /// <summary>Record id.</summary>
    public required int Id { get; init; }

    /// <summary>Name; falls back to the display name for addresses without a name of their own.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the contact is a company rather than an individual.</summary>
    public bool IsCompany { get; init; }

    /// <summary>E-mail address.</summary>
    public string? Email { get; init; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; init; }

    /// <summary>Tax identification number (the <c>vat</c> field: NIT, RUT, RFC, CIF...).</summary>
    public string? TaxId { get; init; }

    /// <summary>Street address.</summary>
    public string? Street { get; init; }

    /// <summary>City.</summary>
    public string? City { get; init; }

    /// <summary>Postal code.</summary>
    public string? Zip { get; init; }

    /// <summary>Country.</summary>
    public OdooReference? Country { get; init; }

    /// <summary>Company the contact works for (the <c>parent_id</c> field), if any.</summary>
    public OdooReference? ParentCompany { get; init; }
}
