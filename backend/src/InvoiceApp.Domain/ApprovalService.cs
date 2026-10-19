namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, with role-based approval limits.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver, ApprovalRole role)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        if (invoice.Amount > 500_000m)
        {
            return HandleHighValueApproval(invoice, approver, role);
        }

        var singleApprovalLimit = role switch
        {
            ApprovalRole.Manager => 50_000m,
            ApprovalRole.Director => 500_000m,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Only Managers and Directors can approve invoices.")
        };

        if (invoice.Amount > singleApprovalLimit)
        {
            throw new InvalidOperationException(
                $"{role} cannot approve an invoice above {singleApprovalLimit:N0} with a single approval.");
        }

        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = null;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice HandleHighValueApproval(Invoice invoice, string approver, ApprovalRole role)
    {
        if (role == ApprovalRole.Manager)
        {
            if (!string.IsNullOrEmpty(invoice.FirstApprovalBy) && invoice.FirstApprovalBy != approver)
            {
                throw new InvalidOperationException("This invoice already has a first approval from a different Manager.");
            }

            if (!string.IsNullOrEmpty(invoice.FirstApprovalBy) && invoice.FirstApprovalBy == approver)
            {
                throw new InvalidOperationException("A different Director is required for the second approval.");
            }

            invoice.FirstApprovalBy = approver;
            invoice.FirstApprovalAt = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.PendingApproval;
            _repository.Update(invoice);
            return invoice;
        }

        if (role != ApprovalRole.Director)
        {
            throw new InvalidOperationException("Only a Manager can record the first approval for high-value invoices.");
        }

        if (string.IsNullOrEmpty(invoice.FirstApprovalBy))
        {
            throw new InvalidOperationException("A Director cannot complete a high-value invoice without a prior Manager approval.");
        }

        if (string.Equals(invoice.FirstApprovalBy, approver, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A different Director is required for the second approval.");
        }

        if (string.IsNullOrEmpty(invoice.SecondApprovalBy))
        {
            invoice.SecondApprovalBy = approver;
            invoice.SecondApprovalAt = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            invoice.DecisionNote = null;
            _repository.Update(invoice);
            return invoice;
        }

        throw new InvalidOperationException("This high-value invoice is already approved.");
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
