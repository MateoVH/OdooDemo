namespace OdooConnector.Invoices;

/// <summary>Status of an invoice (the <c>state</c> field of <c>account.move</c>).</summary>
public enum InvoiceState
{
    /// <summary>A state this library does not know about.</summary>
    Unknown = 0,

    /// <summary>Draft: editable and without an official number yet.</summary>
    Draft,

    /// <summary>Posted (confirmed): numbered and recorded in the accounting.</summary>
    Posted,

    /// <summary>Cancelled.</summary>
    Cancelled,
}
