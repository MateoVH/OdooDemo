namespace OdooConnector;

/// <summary>
/// Connection settings for an Odoo instance. Usually bound from the <c>"Odoo"</c> configuration section.
/// </summary>
public sealed class OdooOptions
{
    /// <summary>Default configuration section name: <c>"Odoo"</c>.</summary>
    public const string SectionName = "Odoo";

    /// <summary>
    /// Root URL of the Odoo server, e.g. <c>https://mycompany.odoo.com</c> or <c>http://localhost:8069</c>.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// Database name. On Odoo Online it is usually the subdomain (<c>mycompany</c>).
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>Login of the user the integration acts as (usually an e-mail address).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// API key of that user, created in Odoo from the user menu (<em>Preferences → Account Security</em>;
    /// <em>My Preferences → Security</em> since Odoo 19). The password also works unless two-factor
    /// authentication is enabled, but an API key is safer and can be revoked on its own.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Describes the connection without revealing the API key, so it is safe to log.</summary>
    public override string ToString() => $"{Url} (database: {Database}, user: {Username}, API key: ***)";
}
