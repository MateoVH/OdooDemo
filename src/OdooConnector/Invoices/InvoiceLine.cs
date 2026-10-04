namespace OdooConnector.Invoices;

/// <summary>A line of an <see cref="Invoice"/> (<c>account.move.line</c>).</summary>
public sealed record InvoiceLine
{
    /// <summary>Record id.</summary>
    public required int Id { get; init; }

    /// <summary>Line label.</summary>
    public string? Description { get; init; }

    /// <summary>Product, if any.</summary>
    public OdooReference? Product { get; init; }

    /// <summary>Quantity.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Discount percentage.</summary>
    public decimal Discount { get; init; }

    /// <summary>Line total without taxes.</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Line total including taxes.</summary>
    public decimal Total { get; init; }

    /// <summary>Ids of the applied taxes.</summary>
    public IReadOnlyList<int> TaxIds { get; init; } = [];
}
