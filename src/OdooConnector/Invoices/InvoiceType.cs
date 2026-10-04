using OdooConnector.Resources;

namespace OdooConnector.Invoices;

/// <summary>Kind of invoice (the <c>move_type</c> field of <c>account.move</c>).</summary>
public enum InvoiceType
{
    /// <summary>Customer invoice (<c>out_invoice</c>).</summary>
    CustomerInvoice,

    /// <summary>Customer credit note (<c>out_refund</c>).</summary>
    CustomerCreditNote,

    /// <summary>Vendor bill (<c>in_invoice</c>).</summary>
    VendorBill,

    /// <summary>Vendor credit note (<c>in_refund</c>).</summary>
    VendorCreditNote,
}

internal static class InvoiceTypeExtensions
{
    /// <summary>Every <c>move_type</c> that corresponds to an <see cref="InvoiceType"/>.</summary>
    public static readonly string[] MoveTypes = ["out_invoice", "out_refund", "in_invoice", "in_refund"];

    public static string ToMoveType(this InvoiceType type) => type switch
    {
        InvoiceType.CustomerInvoice => "out_invoice",
        InvoiceType.CustomerCreditNote => "out_refund",
        InvoiceType.VendorBill => "in_invoice",
        InvoiceType.VendorCreditNote => "in_refund",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, Strings.UnknownInvoiceType),
    };

    public static InvoiceType FromMoveType(string? moveType) => moveType switch
    {
        "out_invoice" => InvoiceType.CustomerInvoice,
        "out_refund" => InvoiceType.CustomerCreditNote,
        "in_invoice" => InvoiceType.VendorBill,
        "in_refund" => InvoiceType.VendorCreditNote,
        _ => throw new OdooException(Strings.NotAnInvoiceMoveType(moveType)),
    };
}
