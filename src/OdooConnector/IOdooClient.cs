namespace OdooConnector;

/// <summary>
/// Low-level access to Odoo's external XML-RPC API. Any model method can be called through
/// <see cref="ExecuteKwAsync"/>; the other methods are typed shortcuts for the most common ORM calls.
/// </summary>
public interface IOdooClient
{
    /// <summary>Returns the server version. Does not require authentication.</summary>
    Task<OdooVersionInfo> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates with the configured credentials and returns the user id. The id is cached, so this only
    /// contacts the server once; other methods call it automatically.
    /// </summary>
    /// <exception cref="OdooAuthenticationException">The credentials were rejected.</exception>
    Task<int> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <paramref name="method"/> on <paramref name="model"/> through <c>execute_kw</c>.
    /// </summary>
    /// <param name="model">Technical model name, e.g. <c>res.partner</c>.</param>
    /// <param name="method">Model method, e.g. <c>search_read</c> or <c>action_post</c>.</param>
    /// <param name="args">Positional arguments.</param>
    /// <param name="kwargs">Keyword arguments (for example <c>fields</c>, <c>limit</c> or <c>context</c>).</param>
    /// <param name="cancellationToken">Cancels the HTTP request.</param>
    /// <returns>The decoded result: numbers, strings, booleans, <c>object?[]</c> lists or dictionaries.</returns>
    /// <exception cref="OdooFaultException">Odoo reported an error.</exception>
    Task<object?> ExecuteKwAsync(
        string model,
        string method,
        IReadOnlyList<object?> args,
        IReadOnlyDictionary<string, object?>? kwargs = null,
        CancellationToken cancellationToken = default);

    /// <summary>Searches records and reads their fields in a single call (<c>search_read</c>).</summary>
    /// <param name="model">Technical model name, e.g. <c>res.partner</c>.</param>
    /// <param name="domain">Filter; <see langword="null"/> or <see cref="OdooDomain.Empty"/> for all records.</param>
    /// <param name="fields">Fields to read. Always list them: <see langword="null"/> reads every field, which is slow.</param>
    /// <param name="limit">Maximum number of records.</param>
    /// <param name="offset">Number of records to skip.</param>
    /// <param name="order">Sort specification, e.g. <c>name asc, id desc</c>.</param>
    /// <param name="cancellationToken">Cancels the HTTP request.</param>
    Task<IReadOnlyList<OdooRecord>> SearchReadAsync(
        string model,
        OdooDomain? domain = null,
        IEnumerable<string>? fields = null,
        int? limit = null,
        int? offset = null,
        string? order = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts the records that match <paramref name="domain"/> (<c>search_count</c>).</summary>
    Task<int> SearchCountAsync(string model, OdooDomain? domain = null, CancellationToken cancellationToken = default);

    /// <summary>Reads the given records (<c>read</c>), in the same order as <paramref name="ids"/>.</summary>
    Task<IReadOnlyList<OdooRecord>> ReadAsync(
        string model,
        IEnumerable<int> ids,
        IEnumerable<string>? fields = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a record (<c>create</c>) and returns its id.</summary>
    /// <param name="model">Technical model name.</param>
    /// <param name="values">Field values; use <see cref="OdooCommand"/> for one2many and many2many fields.</param>
    /// <param name="cancellationToken">Cancels the HTTP request.</param>
    Task<int> CreateAsync(
        string model,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the given records with the same values (<c>write</c>).</summary>
    Task WriteAsync(
        string model,
        IEnumerable<int> ids,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the given records (<c>unlink</c>).</summary>
    Task UnlinkAsync(string model, IEnumerable<int> ids, CancellationToken cancellationToken = default);
}
