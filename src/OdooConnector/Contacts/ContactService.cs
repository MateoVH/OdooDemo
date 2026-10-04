namespace OdooConnector.Contacts;

/// <summary>Default <see cref="IContactService"/> implementation, built on <see cref="IOdooClient"/>.</summary>
/// <param name="client">Client used to talk to Odoo.</param>
public sealed class ContactService(IOdooClient client) : IContactService
{
    internal const string Model = "res.partner";

    // Only fields defined by the base module, so it works even without the Contacts or Invoicing apps.
    internal static readonly string[] Fields =
        ["name", "display_name", "is_company", "email", "phone", "vat", "street", "city", "zip", "country_id", "parent_id"];

    private static readonly bool[] ActiveAndArchived = [true, false];

    private readonly IOdooClient _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Contact>> SearchAsync(ContactQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new ContactQuery();

        var records = await _client
            .SearchReadAsync(Model, BuildDomain(query), Fields, query.Limit, query.Offset, query.OrderBy, cancellationToken)
            .ConfigureAwait(false);
        return records.Select(Map).ToArray();
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(ContactQuery? query = null, CancellationToken cancellationToken = default) =>
        _client.SearchCountAsync(Model, BuildDomain(query ?? new ContactQuery()), cancellationToken);

    /// <inheritdoc/>
    public async Task<Contact?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        // Mentioning "active" in the domain disables Odoo's implicit filter that hides archived records.
        var domain = OdooDomain.Where("id", "=", id).And("active", "in", ActiveAndArchived);
        var records = await _client
            .SearchReadAsync(Model, domain, Fields, limit: 1, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return records.Count == 0 ? null : Map(records[0]);
    }

    internal static OdooDomain BuildDomain(ContactQuery query)
    {
        var domain = OdooDomain.Empty;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim();
            domain = domain.And(OdooDomain.Where("name", "ilike", text).Or("email", "ilike", text));
        }

        if (query.IsCompany is { } isCompany)
        {
            domain = domain.And("is_company", "=", isCompany);
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            domain = domain.And("country_id.code", "=", query.CountryCode.Trim().ToUpperInvariant());
        }

        return query.Filter is null ? domain : domain.And(query.Filter);
    }

    internal static Contact Map(OdooRecord record) => new()
    {
        Id = record.Id,
        Name = record.GetString("name") ?? record.GetString("display_name") ?? string.Empty,
        IsCompany = record.GetBoolean("is_company"),
        Email = record.GetString("email"),
        Phone = record.GetString("phone"),
        TaxId = record.GetString("vat"),
        Street = record.GetString("street"),
        City = record.GetString("city"),
        Zip = record.GetString("zip"),
        Country = record.GetReference("country_id"),
        ParentCompany = record.GetReference("parent_id"),
    };
}
