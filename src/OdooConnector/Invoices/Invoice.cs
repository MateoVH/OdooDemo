namespace OdooConnector.Invoices;

/// <summary>An invoice read from Odoo (<c>account.move</c>), with the totals computed by Odoo.</summary>
public sealed record Invoice
{
    /// <summary>Record id.</summary>
    public required int Id { get; init; }

    /// <summary>Invoice number, e.g. <c>INV/2026/00042</c>; <see langword="null"/> while the draft has no number.</summary>
    public string? Number { get; init; }

    /// <summary>Kind of invoice.</summary>
    public required InvoiceType Type { get; init; }

    /// <summary>Draft, posted or cancelled.</summary>
    public required InvoiceState State { get; init; }

    /// <summary>Customer or vendor.</summary>
    public OdooReference? Partner { get; init; }

    /// <summary>Invoice date.</summary>
    public DateOnly? InvoiceDate { get; init; }

    /// <summary>Due date.</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>Customer reference (the <c>ref</c> field).</summary>
    public string? Reference { get; init; }

    /// <summary>Currency; its name is the ISO code, e.g. <c>USD</c>.</summary>
    public OdooReference? Currency { get; init; }

    /// <summary>Total without taxes.</summary>
    public decimal AmountUntaxed { get; init; }

    /// <summary>Total taxes.</summary>
    public decimal AmountTax { get; init; }

    /// <summary>Total including taxes.</summary>
    public decimal AmountTotal { get; init; }

    /// <summary>Amount still to be paid (the <c>amount_residual</c> field).</summary>
    public decimal AmountDue { get; init; }

    /// <summary>Payment status as reported by Odoo: <c>not_paid</c>, <c>partial</c>, <c>paid</c>...</summary>
    public string? PaymentState { get; init; }

    /// <summary>Invoice lines.</summary>
    public IReadOnlyList<InvoiceLine> Lines { get; init; } = [];
}
