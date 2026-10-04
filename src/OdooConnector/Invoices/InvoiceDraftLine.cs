namespace OdooConnector.Invoices;

/// <summary>A line of an <see cref="InvoiceDraft"/>. It needs a <see cref="Description"/>, a <see cref="ProductId"/> or both.</summary>
public sealed record InvoiceDraftLine
{
    /// <summary>Line label. When omitted, Odoo uses the product name.</summary>
    public string? Description { get; init; }

    /// <summary>Product id; Odoo uses it to fill in the price, taxes and income account that are not given.</summary>
    public int? ProductId { get; init; }

    /// <summary>Quantity (1 by default).</summary>
    public decimal Quantity { get; init; } = 1;

    /// <summary>Unit price. When omitted, Odoo uses the product price.</summary>
    public decimal? UnitPrice { get; init; }

    /// <summary>Discount percentage, from 0 to 100.</summary>
    public decimal Discount { get; init; }

    /// <summary>
    /// Ids of the taxes to apply. <see langword="null"/> lets Odoo apply the product or company defaults;
    /// an empty list creates the line without taxes.
    /// </summary>
    public IReadOnlyList<int>? TaxIds { get; init; }
}
