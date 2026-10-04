namespace OdooConnector.Contacts;

/// <summary>Reads contacts (<c>res.partner</c>) from Odoo.</summary>
public interface IContactService
{
    /// <summary>Returns the contacts that match <paramref name="query"/> (all active contacts, up to 100, by default).</summary>
    Task<IReadOnlyList<Contact>> SearchAsync(ContactQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Counts the contacts that match <paramref name="query"/>, ignoring its paging.</summary>
    Task<int> CountAsync(ContactQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a contact by id, including archived ones; <see langword="null"/> if it does not exist.</summary>
    Task<Contact?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
