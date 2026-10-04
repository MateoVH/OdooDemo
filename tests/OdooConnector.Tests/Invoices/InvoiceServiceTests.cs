using OdooConnector.Invoices;
using OdooConnector.Tests.Infrastructure;

namespace OdooConnector.Tests.Invoices;

public sealed class InvoiceServiceTests
{
    private readonly FakeOdooServer _odoo = new();
    private readonly InvoiceService _invoices;

    public InvoiceServiceTests() => _invoices = new InvoiceService(_odoo.CreateClient());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_SendsTheInvoiceAndItsLinesInOneCall()
    {
        _odoo.On("account.move", "create", 42);

        var id = await _invoices.CreateAsync(
            new InvoiceDraft
            {
                PartnerId = 14,
                InvoiceDate = new DateOnly(2026, 10, 4),
                DueDate = new DateOnly(2026, 11, 3),
                Reference = "PO-1001",
                CurrencyId = 2,
                JournalId = 1,
                Lines =
                [
                    new InvoiceDraftLine { Description = "Consultoría Odoo (horas)", Quantity = 10, UnitPrice = 85m, TaxIds = [1] },
                    new InvoiceDraftLine { ProductId = 5, Quantity = 2, Discount = 10 },
                ],
            },
            Ct);

        Assert.Equal(42, id);
        var call = _odoo.ModelCalls.Single();
        Assert.Equal("create", call.Method);
        Assert.Equal(
            "[{'move_type': 'out_invoice', 'partner_id': 14, 'invoice_line_ids': [" +
            "[0, 0, {'quantity': 10.0, 'name': 'Consultoría Odoo (horas)', 'price_unit': 85.0, 'tax_ids': [[6, 0, [1]]]}], " +
            "[0, 0, {'quantity': 2.0, 'product_id': 5, 'discount': 10.0}]], " +
            "'invoice_date': '2026-10-04', 'invoice_date_due': '2026-11-03', 'ref': 'PO-1001', 'currency_id': 2, 'journal_id': 1}]",
            Py.Repr(call.Args));
    }

    [Fact]
    public async Task CreateAsync_LeavesOptionalValuesToOdoo()
    {
        _odoo.On("account.move", "create", 43);

        await _invoices.CreateAsync(
            new InvoiceDraft { PartnerId = 14, Lines = [new InvoiceDraftLine { Description = "Soporte", UnitPrice = 50m }] },
            Ct);

        Assert.Equal(
            "[{'move_type': 'out_invoice', 'partner_id': 14, 'invoice_line_ids': [[0, 0, {'quantity': 1.0, 'name': 'Soporte', 'price_unit': 50.0}]]}]",
            Py.Repr(_odoo.ModelCalls.Single().Args));
    }

    [Fact]
    public async Task CreateAsync_AnEmptyTaxListCreatesTheLineWithoutTaxes()
    {
        _odoo.On("account.move", "create", 44);

        await _invoices.CreateAsync(
            new InvoiceDraft { PartnerId = 14, Lines = [new InvoiceDraftLine { Description = "Exento", UnitPrice = 10m, TaxIds = [] }] },
            Ct);

        Assert.Contains("'tax_ids': [[6, 0, []]]", Py.Repr(_odoo.ModelCalls.Single().Args), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InvoiceType.CustomerInvoice, "out_invoice")]
    [InlineData(InvoiceType.CustomerCreditNote, "out_refund")]
    [InlineData(InvoiceType.VendorBill, "in_invoice")]
    [InlineData(InvoiceType.VendorCreditNote, "in_refund")]
    public async Task CreateAsync_MapsTheInvoiceType(InvoiceType type, string moveType)
    {
        _odoo.On("account.move", "create", 45);

        await _invoices.CreateAsync(
            new InvoiceDraft { PartnerId = 14, Type = type, Lines = [new InvoiceDraftLine { Description = "Item", UnitPrice = 1m }] },
            Ct);

        Assert.StartsWith($"[{{'move_type': '{moveType}'", Py.Repr(_odoo.ModelCalls.Single().Args), StringComparison.Ordinal);
    }

    public static TheoryData<string, InvoiceDraft> IncompleteDrafts => new()
    {
        { "PartnerId", new InvoiceDraft { PartnerId = 0, Lines = [new InvoiceDraftLine { Description = "Item" }] } },
        { "at least one line", new InvoiceDraft { PartnerId = 14, Lines = [] } },
        { "Description or a ProductId", new InvoiceDraft { PartnerId = 14, Lines = [new InvoiceDraftLine { Description = " ", UnitPrice = 5m }] } },
        { "between 0 and 100", new InvoiceDraft { PartnerId = 14, Lines = [new InvoiceDraftLine { Description = "Item", Discount = 120 }] } },
    };

    [Theory]
    [MemberData(nameof(IncompleteDrafts))]
    public async Task CreateAsync_RejectsIncompleteDraftsBeforeCallingOdoo(string expectedError, InvoiceDraft draft)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _invoices.CreateAsync(draft, Ct));

        Assert.Contains(expectedError, exception.Message, StringComparison.Ordinal);
        Assert.Empty(_odoo.Calls);
    }

    [Fact]
    public async Task PostAsync_CallsActionPost()
    {
        _odoo.On("account.move", "action_post", false);

        await _invoices.PostAsync(42, Ct);

        var call = _odoo.ModelCalls.Single();
        Assert.Equal("action_post", call.Method);
        Assert.Equal("[[42]]", Py.Repr(call.Args));
    }

    [Fact]
    public async Task PostAsync_SurfacesOdooValidationErrors()
    {
        _odoo.OnFault("account.move", "action_post", 2, "The field 'Customer' is required to post the invoice.");

        var exception = await Assert.ThrowsAsync<OdooFaultException>(() => _invoices.PostAsync(42, Ct));

        Assert.Equal(OdooFaultKind.UserError, exception.Kind);
        Assert.Equal("The field 'Customer' is required to post the invoice.", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ReadsTheInvoiceAndItsLines()
    {
        _odoo
            .On("account.move", "search_read", new object[] { InvoiceRecord(name: "INV/2026/00042", state: "posted", paymentState: "not_paid") })
            .On("account.move.line", "read", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 101,
                    ["name"] = "Consultoría Odoo (horas)",
                    ["product_id"] = false,
                    ["quantity"] = 10.0,
                    ["price_unit"] = 85.0,
                    ["discount"] = 0.0,
                    ["tax_ids"] = new object[] { 1 },
                    ["price_subtotal"] = 850.0,
                    ["price_total"] = 977.5,
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 102,
                    ["name"] = "[E-COM11] Cabinet with Doors",
                    ["product_id"] = new object[] { 5, "[E-COM11] Cabinet with Doors" },
                    ["quantity"] = 2.0,
                    ["price_unit"] = 140.0,
                    ["discount"] = 10.0,
                    ["tax_ids"] = Array.Empty<object>(),
                    ["price_subtotal"] = 252.0,
                    ["price_total"] = 252.0,
                },
            });

        var invoice = await _invoices.GetByIdAsync(42, Ct);

        Assert.NotNull(invoice);
        Assert.Equal(42, invoice.Id);
        Assert.Equal("INV/2026/00042", invoice.Number);
        Assert.Equal(InvoiceType.CustomerInvoice, invoice.Type);
        Assert.Equal(InvoiceState.Posted, invoice.State);
        Assert.Equal(new OdooReference(14, "Azure Interior"), invoice.Partner);
        Assert.Equal(new DateOnly(2026, 10, 4), invoice.InvoiceDate);
        Assert.Equal(new DateOnly(2026, 11, 3), invoice.DueDate);
        Assert.Equal("PO-1001", invoice.Reference);
        Assert.Equal("USD", invoice.Currency?.Name);
        Assert.Equal(1102m, invoice.AmountUntaxed);
        Assert.Equal(127.5m, invoice.AmountTax);
        Assert.Equal(1229.5m, invoice.AmountTotal);
        Assert.Equal(1229.5m, invoice.AmountDue);
        Assert.Equal("not_paid", invoice.PaymentState);

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(
            new InvoiceLine
            {
                Id = 102,
                Description = "[E-COM11] Cabinet with Doors",
                Product = new OdooReference(5, "[E-COM11] Cabinet with Doors"),
                Quantity = 2m,
                UnitPrice = 140m,
                Discount = 10m,
                Subtotal = 252m,
                Total = 252m,
                TaxIds = invoice.Lines[1].TaxIds,
            },
            invoice.Lines[1]);
        Assert.Equal(new[] { 1 }, invoice.Lines[0].TaxIds);

        var (search, read) = (_odoo.ModelCalls[0], _odoo.ModelCalls[1]);
        Assert.Equal(
            "[['&', ['id', '=', 42], ['move_type', 'in', ['out_invoice', 'out_refund', 'in_invoice', 'in_refund']]]]",
            Py.Repr(search.Args));
        Assert.Equal("[[101, 102]]", Py.Repr(read.Args));
        Assert.Equal(
            "{'fields': ['name', 'product_id', 'quantity', 'price_unit', 'discount', 'tax_ids', 'price_subtotal', 'price_total']}",
            Py.Repr(read.Kwargs));
    }

    [Fact]
    public async Task GetByIdAsync_DraftsWithoutNumberHaveNoNumber()
    {
        _odoo
            .On("account.move", "search_read", new object[] { InvoiceRecord(name: "/", state: "draft", paymentState: "not_paid") })
            .On("account.move.line", "read", Array.Empty<object>());

        var invoice = await _invoices.GetByIdAsync(42, Ct);

        Assert.Null(invoice?.Number);
        Assert.Equal(InvoiceState.Draft, invoice?.State);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownIds()
    {
        _odoo.On("account.move", "search_read", Array.Empty<object>());

        Assert.Null(await _invoices.GetByIdAsync(404, Ct));
        Assert.Single(_odoo.ModelCalls);
    }

    private static Dictionary<string, object?> InvoiceRecord(string name, string state, string paymentState) => new()
    {
        ["id"] = 42,
        ["name"] = name,
        ["move_type"] = "out_invoice",
        ["state"] = state,
        ["partner_id"] = new object[] { 14, "Azure Interior" },
        ["invoice_date"] = "2026-10-04",
        ["invoice_date_due"] = "2026-11-03",
        ["ref"] = "PO-1001",
        ["currency_id"] = new object[] { 2, "USD" },
        ["amount_untaxed"] = 1102.0,
        ["amount_tax"] = 127.5,
        ["amount_total"] = 1229.5,
        ["amount_residual"] = 1229.5,
        ["payment_state"] = paymentState,
        ["invoice_line_ids"] = new object[] { 101, 102 },
    };
}
