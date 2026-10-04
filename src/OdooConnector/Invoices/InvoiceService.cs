using OdooConnector.Resources;

namespace OdooConnector.Invoices;

/// <summary>Default <see cref="IInvoiceService"/> implementation, built on <see cref="IOdooClient"/>.</summary>
/// <param name="client">Client used to talk to Odoo.</param>
public sealed class InvoiceService(IOdooClient client) : IInvoiceService
{
    internal const string InvoiceModel = "account.move";
    internal const string LineModel = "account.move.line";

    internal static readonly string[] InvoiceFields =
    [
        "name", "move_type", "state", "partner_id", "invoice_date", "invoice_date_due", "ref", "currency_id",
        "amount_untaxed", "amount_tax", "amount_total", "amount_residual", "payment_state", "invoice_line_ids",
    ];

    internal static readonly string[] LineFields =
        ["name", "product_id", "quantity", "price_unit", "discount", "tax_ids", "price_subtotal", "price_total"];

    private readonly IOdooClient _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public Task<int> CreateAsync(InvoiceDraft draft, CancellationToken cancellationToken = default)
    {
        Validate(draft);
        return _client.CreateAsync(InvoiceModel, ToValues(draft), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PostAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(invoiceId);

        // action_post raises a UserError when the invoice cannot be posted. Its return value is False, or
        // (vendor bills, Odoo 18+) an optional "auto-post" suggestion returned after posting, so it is ignored.
        await _client
            .ExecuteKwAsync(InvoiceModel, "action_post", [new[] { invoiceId }], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Invoice?> GetByIdAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(invoiceId);

        // Restricting move_type makes journal entries and receipts with that id come back as "not found".
        var domain = OdooDomain.Where("id", "=", invoiceId).And("move_type", "in", InvoiceTypeExtensions.MoveTypes);
        var invoices = await _client
            .SearchReadAsync(InvoiceModel, domain, InvoiceFields, limit: 1, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (invoices.Count == 0)
        {
            return null;
        }

        var invoice = invoices[0];
        var lines = await _client
            .ReadAsync(LineModel, invoice.GetIds("invoice_line_ids"), LineFields, cancellationToken)
            .ConfigureAwait(false);
        return Map(invoice, lines);
    }

    internal static void Validate(InvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.PartnerId <= 0)
        {
            throw new ArgumentException(Strings.InvoicePartnerRequired, nameof(draft));
        }

        if (draft.Lines is null || draft.Lines.Count == 0)
        {
            throw new ArgumentException(Strings.InvoiceLinesRequired, nameof(draft));
        }

        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i] ?? throw new ArgumentException(Strings.InvoiceLineNull(i + 1), nameof(draft));

            if (line.ProductId is null && string.IsNullOrWhiteSpace(line.Description))
            {
                throw new ArgumentException(Strings.InvoiceLineDescriptionRequired(i + 1), nameof(draft));
            }

            if (line.Discount is < 0 or > 100)
            {
                throw new ArgumentException(Strings.InvoiceLineDiscountOutOfRange(i + 1), nameof(draft));
            }
        }
    }

    internal static Dictionary<string, object?> ToValues(InvoiceDraft draft)
    {
        var values = new Dictionary<string, object?>
        {
            ["move_type"] = draft.Type.ToMoveType(),
            ["partner_id"] = draft.PartnerId,
            // (0, 0, values) commands create the lines together with the invoice in a single call.
            ["invoice_line_ids"] = draft.Lines.Select(line => OdooCommand.Create(ToValues(line))).ToArray(),
        };

        if (draft.InvoiceDate is { } invoiceDate)
        {
            values["invoice_date"] = invoiceDate;
        }

        if (draft.DueDate is { } dueDate)
        {
            values["invoice_date_due"] = dueDate;
        }

        if (!string.IsNullOrWhiteSpace(draft.Reference))
        {
            values["ref"] = draft.Reference;
        }

        if (draft.CurrencyId is { } currencyId)
        {
            values["currency_id"] = currencyId;
        }

        if (draft.JournalId is { } journalId)
        {
            values["journal_id"] = journalId;
        }

        return values;
    }

    // Optional values are left out instead of being sent empty, so Odoo can compute them from the product.
    internal static Dictionary<string, object?> ToValues(InvoiceDraftLine line)
    {
        var values = new Dictionary<string, object?> { ["quantity"] = line.Quantity };

        if (!string.IsNullOrWhiteSpace(line.Description))
        {
            values["name"] = line.Description;
        }

        if (line.ProductId is { } productId)
        {
            values["product_id"] = productId;
        }

        if (line.UnitPrice is { } unitPrice)
        {
            values["price_unit"] = unitPrice;
        }

        if (line.Discount != 0)
        {
            values["discount"] = line.Discount;
        }

        if (line.TaxIds is { } taxIds)
        {
            values["tax_ids"] = new[] { OdooCommand.Set(taxIds) };
        }

        return values;
    }

    internal static Invoice Map(OdooRecord invoice, IReadOnlyList<OdooRecord> lines) => new()
    {
        Id = invoice.Id,
        // Odoo uses "/" as the name of drafts that have not been numbered yet.
        Number = invoice.GetString("name") is { } name && name != "/" ? name : null,
        Type = InvoiceTypeExtensions.FromMoveType(invoice.GetString("move_type")),
        State = invoice.GetString("state") switch
        {
            "draft" => InvoiceState.Draft,
            "posted" => InvoiceState.Posted,
            "cancel" => InvoiceState.Cancelled,
            _ => InvoiceState.Unknown,
        },
        Partner = invoice.GetReference("partner_id"),
        InvoiceDate = invoice.GetDate("invoice_date"),
        DueDate = invoice.GetDate("invoice_date_due"),
        Reference = invoice.GetString("ref"),
        Currency = invoice.GetReference("currency_id"),
        AmountUntaxed = invoice.GetDecimal("amount_untaxed"),
        AmountTax = invoice.GetDecimal("amount_tax"),
        AmountTotal = invoice.GetDecimal("amount_total"),
        AmountDue = invoice.GetDecimal("amount_residual"),
        PaymentState = invoice.GetString("payment_state"),
        Lines = lines.Select(MapLine).ToArray(),
    };

    private static InvoiceLine MapLine(OdooRecord line) => new()
    {
        Id = line.Id,
        Description = line.GetString("name"),
        Product = line.GetReference("product_id"),
        Quantity = line.GetDecimal("quantity"),
        UnitPrice = line.GetDecimal("price_unit"),
        Discount = line.GetDecimal("discount"),
        Subtotal = line.GetDecimal("price_subtotal"),
        Total = line.GetDecimal("price_total"),
        TaxIds = line.GetIds("tax_ids"),
    };
}
