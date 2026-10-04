namespace OdooConnector.Contacts;

/// <summary>Filters and paging for <see cref="IContactService.SearchAsync"/>. Every filter is optional.</summary>
public sealed record ContactQuery
{
    /// <summary>Text contained in the name or the e-mail (case-insensitive).</summary>
    public string? Search { get; init; }

    /// <summary><see langword="true"/> for companies only, <see langword="false"/> for individuals only.</summary>
    public bool? IsCompany { get; init; }

    /// <summary>ISO country code, e.g. <c>CO</c> or <c>MX</c>.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Additional conditions, combined with the filters above using AND.</summary>
    public OdooDomain? Filter { get; init; }

    /// <summary>Maximum number of contacts to return (100 by default); <see langword="null"/> for no limit.</summary>
    public int? Limit { get; init; } = 100;

    /// <summary>Number of contacts to skip, for paging.</summary>
    public int Offset { get; init; }

    /// <summary>Sort order, e.g. <c>name asc</c>. Uses Odoo's default order when <see langword="null"/>.</summary>
    public string? OrderBy { get; init; }
}
