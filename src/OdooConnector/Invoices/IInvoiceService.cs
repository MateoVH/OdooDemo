namespace OdooConnector.Invoices;

/// <summary>Creates, posts and reads invoices (<c>account.move</c>). Requires the Invoicing or Accounting app.</summary>
public interface IInvoiceService
{
    /// <summary>Creates a draft invoice and returns its id.</summary>
    /// <exception cref="ArgumentException">The draft is incomplete (no lines, no partner, a line without description or product...).</exception>
    /// <exception cref="OdooFaultException">Odoo rejected the invoice.</exception>
    Task<int> CreateAsync(InvoiceDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts (confirms) a draft invoice (<c>action_post</c>): Odoo assigns its number and records it in the accounting.
    /// </summary>
    /// <exception cref="OdooFaultException">Odoo could not post the invoice, e.g. because a required field is missing.</exception>
    Task PostAsync(int invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Returns an invoice with its lines; <see langword="null"/> if there is no invoice with that id.</summary>
    Task<Invoice?> GetByIdAsync(int invoiceId, CancellationToken cancellationToken = default);
}
