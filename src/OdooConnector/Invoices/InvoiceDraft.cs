namespace OdooConnector.Invoices;

/// <summary>Data needed to create an invoice. Odoo creates it as a draft; post it with <see cref="IInvoiceService.PostAsync"/>.</summary>
public sealed record InvoiceDraft
{
    /// <summary>Id of the customer or vendor (<c>res.partner</c>).</summary>
    public required int PartnerId { get; init; }

    /// <summary>Kind of invoice; a customer invoice by default.</summary>
    public InvoiceType Type { get; init; } = InvoiceType.CustomerInvoice;

    /// <summary>Invoice date. When omitted, Odoo sets it to the posting date.</summary>
    public DateOnly? InvoiceDate { get; init; }

    /// <summary>Due date. When omitted, Odoo computes it from the payment terms.</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>Customer reference, e.g. a purchase order number (the <c>ref</c> field).</summary>
    public string? Reference { get; init; }

    /// <summary>Currency id; the company currency when omitted.</summary>
    public int? CurrencyId { get; init; }

    /// <summary>Journal id; the default sales or purchase journal when omitted.</summary>
    public int? JournalId { get; init; }

    /// <summary>Invoice lines; at least one is required.</summary>
    public required IReadOnlyList<InvoiceDraftLine> Lines { get; init; }
}
