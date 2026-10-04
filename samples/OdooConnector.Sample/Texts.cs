using OdooConnector;
using OdooConnector.Invoices;

/// <summary>Console texts of the demo, in Spanish or English.</summary>
internal sealed class Texts(bool spanish)
{
    public string Server(string version, Uri? url) =>
        spanish ? $"Odoo {version} en {url}" : $"Odoo {version} at {url}";

    public string Connected(string database, string username, int uid) =>
        spanish ? $"Conectado a la base '{database}' como '{username}' (uid {uid})" : $"Connected to database '{database}' as '{username}' (uid {uid})";

    public string Companies(int shown, int total) =>
        spanish ? $"Empresas ({shown} de {total}):" : $"Companies ({shown} of {total}):";

    public string NameColumn => spanish ? "Nombre" : "Name";

    public string CountryColumn => spanish ? "País" : "Country";

    public string CreateInvoiceHint =>
        spanish ? "Para crear una factura de prueba: dotnet run -- --create-invoice [--post]" : "To create a test invoice: dotnet run -- --create-invoice [--post]";

    public string NoCompanies =>
        spanish ? "No hay empresas para facturar. Crea una en Odoo (Contactos) y vuelve a intentarlo." : "There are no companies to invoice. Create one in Odoo (Contacts) and try again.";

    public string ConsultingLine => spanish ? "Consultoría funcional Odoo (horas)" : "Odoo functional consulting (hours)";

    public string IntegrationLine => spanish ? "Integración .NET ↔ Odoo vía XML-RPC" : ".NET ↔ Odoo integration via XML-RPC";

    public string DraftCreated(int id, string customer) =>
        spanish ? $"Factura borrador creada (id {id}) para {customer}" : $"Draft invoice created (id {id}) for {customer}";

    public string InvoiceNotFound(int id) =>
        spanish ? $"No se encontró la factura {id} recién creada." : $"The invoice {id} that was just created was not found.";

    public string Number => spanish ? "Número" : "Number";

    public string NoNumber => spanish ? "(sin número: borrador)" : "(no number yet: draft)";

    public string Status => spanish ? "Estado" : "Status";

    public string Payment => spanish ? "Pago" : "Payment";

    public string Untaxed => spanish ? "Subtotal" : "Untaxed";

    public string Taxes => spanish ? "Impuestos" : "Taxes";

    public string Total => "Total";

    public string StateName(InvoiceState state) => (state, spanish) switch
    {
        (InvoiceState.Draft, true) => "Borrador",
        (InvoiceState.Posted, true) => "Publicada",
        (InvoiceState.Cancelled, true) => "Cancelada",
        _ => state.ToString(),
    };

    public string MissingConfiguration =>
        spanish ? "Falta configurar la conexión con Odoo:" : "The connection to Odoo is not configured:";

    public string ConfigureWithUserSecrets =>
        spanish ? "Configúrala con user-secrets, por ejemplo:" : "Set it with user-secrets, for example:";

    public string ApiKeyPlaceholder => spanish ? "<tu-api-key>" : "<your-api-key>";

    public string CredentialsRejected(string message) =>
        spanish ? $"Credenciales rechazadas: {message}" : $"Credentials rejected: {message}";

    public string OdooRejected(OdooFaultKind kind, string message) =>
        spanish ? $"Odoo rechazó la operación ({kind}): {message}" : $"Odoo rejected the operation ({kind}): {message}";

    public string ConnectionError(string message) =>
        spanish ? $"Error de conexión: {message}" : $"Connection error: {message}";
}
