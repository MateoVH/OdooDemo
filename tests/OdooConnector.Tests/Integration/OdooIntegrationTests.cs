using OdooConnector.Contacts;
using OdooConnector.Invoices;

namespace OdooConnector.Tests.Integration;

/// <summary>
/// End-to-end tests against a real Odoo server. They are skipped unless <c>ODOO_INTEGRATION_TESTS=1</c> and the
/// connection variables (<c>ODOO__URL</c>, <c>ODOO__DATABASE</c>, <c>ODOO__USERNAME</c>, <c>ODOO__APIKEY</c>) are set.
/// Use a test database: they create a contact and a draft invoice, and delete both at the end.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OdooIntegrationTests : IDisposable
{
    private const string SkipReason =
        "Set ODOO_INTEGRATION_TESTS=1 and ODOO__URL, ODOO__DATABASE, ODOO__USERNAME and ODOO__APIKEY to run against a real Odoo.";

    private static readonly OdooOptions? Options = ReadOptions();

    private readonly HttpClient _http = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConnectsAndReadsContacts()
    {
        Assert.SkipWhen(Options is null, SkipReason);
        var client = new OdooClient(_http, Options!);

        var version = await client.GetVersionAsync(Ct);
        var uid = await client.AuthenticateAsync(Ct);
        var contacts = await new ContactService(client).SearchAsync(new ContactQuery { Limit = 5 }, Ct);

        Assert.NotNull(version.MajorVersion);
        Assert.True(uid > 0);
        Assert.InRange(contacts.Count, 0, 5);
        Assert.All(contacts, contact => Assert.True(contact.Id > 0));
    }

    [Fact]
    public async Task CreatesReadsAndDeletesADraftInvoice()
    {
        Assert.SkipWhen(Options is null, SkipReason);
        var client = new OdooClient(_http, Options!);
        var invoices = new InvoiceService(client);

        var partnerId = await client.CreateAsync(
            "res.partner",
            new Dictionary<string, object?> { ["name"] = $"OdooConnector integration test {Guid.NewGuid():N}", ["is_company"] = true },
            Ct);
        try
        {
            var invoiceId = await invoices.CreateAsync(
                new InvoiceDraft
                {
                    PartnerId = partnerId,
                    Reference = "ODOO-CONNECTOR-IT",
                    Lines =
                    [
                        new InvoiceDraftLine { Description = "Integration test service", Quantity = 2, UnitPrice = 50m, TaxIds = [] },
                        new InvoiceDraftLine { Description = "Integration test discount", Quantity = 1, UnitPrice = 100m, Discount = 10, TaxIds = [] },
                    ],
                },
                Ct);
            try
            {
                var invoice = await invoices.GetByIdAsync(invoiceId, Ct);

                Assert.NotNull(invoice);
                Assert.Equal(InvoiceState.Draft, invoice.State);
                Assert.Equal(partnerId, invoice.Partner?.Id);
                Assert.Equal("ODOO-CONNECTOR-IT", invoice.Reference);
                Assert.Equal(190m, invoice.AmountUntaxed);
                Assert.Equal(0m, invoice.AmountTax);
                Assert.Equal(190m, invoice.AmountTotal);
                Assert.Equal(new[] { 100m, 90m }, invoice.Lines.Select(line => line.Subtotal));
            }
            finally
            {
                await client.UnlinkAsync("account.move", [invoiceId], Ct);
            }
        }
        finally
        {
            await client.UnlinkAsync("res.partner", [partnerId], Ct);
        }
    }

    public void Dispose() => _http.Dispose();

    private static OdooOptions? ReadOptions()
    {
        var enabled = Environment.GetEnvironmentVariable("ODOO_INTEGRATION_TESTS");
        if (enabled is not ("1" or "true" or "True"))
        {
            return null;
        }

        var url = Environment.GetEnvironmentVariable("ODOO__URL");
        var options = new OdooOptions
        {
            Url = url is null ? null : new Uri(url),
            Database = Environment.GetEnvironmentVariable("ODOO__DATABASE") ?? string.Empty,
            Username = Environment.GetEnvironmentVariable("ODOO__USERNAME") ?? string.Empty,
            ApiKey = Environment.GetEnvironmentVariable("ODOO__APIKEY") ?? string.Empty,
        };

        return options.Url is null ? null : options;
    }
}
