// Demo of the connector: shows the Odoo version, lists companies and, on request, creates an invoice.
//
//   dotnet run --project samples/OdooConnector.Sample                              -> read only
//   dotnet run --project samples/OdooConnector.Sample -- --create-invoice          -> creates a draft invoice
//   dotnet run --project samples/OdooConnector.Sample -- --create-invoice --post   -> ...and posts it
//   options: --lang es|en (defaults to the system language), --verbose (logs every RPC call)
//
// The connection is read from appsettings.json, user-secrets or environment variables (ODOO__URL, ODOO__APIKEY...).

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OdooConnector;
using OdooConnector.Contacts;
using OdooConnector.Invoices;

Console.OutputEncoding = Encoding.UTF8;

var createInvoice = args.Contains("--create-invoice");
var postInvoice = args.Contains("--post");
var verbose = args.Contains("--verbose");

// The UI culture also selects the language of the connector's own messages (English or Spanish).
var language = ReadOption(args, "--lang") ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
var spanish = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(spanish ? "es" : "en");
var text = new Texts(spanish);

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(logging => logging
    .AddSimpleConsole(console => console.SingleLine = true)
    .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning)
    .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));
services.AddOdooConnector(configuration.GetSection(OdooOptions.SectionName));

await using var provider = services.BuildServiceProvider();

try
{
    var odoo = provider.GetRequiredService<IOdooClient>();
    var contacts = provider.GetRequiredService<IContactService>();
    var invoices = provider.GetRequiredService<IInvoiceService>();
    var settings = provider.GetRequiredService<IOptions<OdooOptions>>().Value;

    var version = await odoo.GetVersionAsync();
    Console.WriteLine(text.Server(version.ServerVersion, settings.Url));

    var uid = await odoo.AuthenticateAsync();
    Console.WriteLine(text.Connected(settings.Database, settings.Username, uid));
    Console.WriteLine();

    var companiesQuery = new ContactQuery { IsCompany = true, Limit = 10, OrderBy = "name asc" };
    var total = await contacts.CountAsync(companiesQuery);
    var companies = await contacts.SearchAsync(companiesQuery);

    Console.WriteLine(text.Companies(companies.Count, total));
    Console.WriteLine($"  {"ID",5}  {text.NameColumn,-32} {"Email",-32} {text.CountryColumn}");
    foreach (var company in companies)
    {
        Console.WriteLine($"  {company.Id,5}  {Fit(company.Name, 32),-32} {Fit(company.Email ?? "-", 32),-32} {company.Country?.Name ?? "-"}");
    }

    if (!createInvoice)
    {
        Console.WriteLine();
        Console.WriteLine(text.CreateInvoiceHint);
        return 0;
    }

    var customer = companies.FirstOrDefault();
    if (customer is null)
    {
        Console.Error.WriteLine(text.NoCompanies);
        return 1;
    }

    var invoiceId = await invoices.CreateAsync(new InvoiceDraft
    {
        PartnerId = customer.Id,
        InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
        DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        Reference = $"DEMO-{DateTime.Now:yyyyMMdd-HHmm}",
        Lines =
        [
            new InvoiceDraftLine { Description = text.ConsultingLine, Quantity = 12, UnitPrice = 85m },
            new InvoiceDraftLine { Description = text.IntegrationLine, Quantity = 1, UnitPrice = 1500m, Discount = 10 },
        ],
    });

    Console.WriteLine();
    Console.WriteLine(text.DraftCreated(invoiceId, customer.Name));

    if (postInvoice)
    {
        await invoices.PostAsync(invoiceId);
    }

    var invoice = await invoices.GetByIdAsync(invoiceId)
        ?? throw new InvalidOperationException(text.InvoiceNotFound(invoiceId));

    var currency = invoice.Currency?.Name ?? string.Empty;
    Console.WriteLine($"  {text.Number,-10} {invoice.Number ?? text.NoNumber}");
    Console.WriteLine($"  {text.Status,-10} {text.StateName(invoice.State)}   {text.Payment}: {invoice.PaymentState ?? "-"}");
    foreach (var line in invoice.Lines)
    {
        Console.WriteLine($"  - {Fit(line.Description ?? "-", 40),-40} {line.Quantity,6:0.##} x {line.UnitPrice,10:N2} = {line.Subtotal,10:N2}");
    }

    Console.WriteLine($"  {text.Untaxed,-10} {invoice.AmountUntaxed,12:N2} {currency}");
    Console.WriteLine($"  {text.Taxes,-10} {invoice.AmountTax,12:N2} {currency}");
    Console.WriteLine($"  {text.Total,-10} {invoice.AmountTotal,12:N2} {currency}");
    return 0;
}
catch (OptionsValidationException exception)
{
    Console.Error.WriteLine(text.MissingConfiguration);
    foreach (var failure in exception.Failures)
    {
        Console.Error.WriteLine($"  - {failure}");
    }

    Console.Error.WriteLine(text.ConfigureWithUserSecrets);
    Console.Error.WriteLine($"  dotnet user-secrets set \"Odoo:ApiKey\" \"{text.ApiKeyPlaceholder}\" --project samples/OdooConnector.Sample");
    return 1;
}
catch (OdooAuthenticationException exception)
{
    Console.Error.WriteLine(text.CredentialsRejected(exception.Message));
    return 1;
}
catch (OdooFaultException exception)
{
    Console.Error.WriteLine(text.OdooRejected(exception.Kind, exception.Message));
    return 1;
}
catch (OdooException exception)
{
    Console.Error.WriteLine(text.ConnectionError(exception.Message));
    return 1;
}

static string? ReadOption(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            return args[i][(name.Length + 1)..];
        }

        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static string Fit(string value, int width) => value.Length <= width ? value : string.Concat(value.AsSpan(0, width - 1), "…");
