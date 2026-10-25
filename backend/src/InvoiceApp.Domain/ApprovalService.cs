namespace InvoiceApp.Domain;

/// <summary>
/// Moves invoices through submission, full or partial approval, and rejection with role-based limits.
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
        Save(invoice);
        return invoice;
    }

    public Invoice Approve(
        string invoiceId,
        string approver,
        ApprovalRole role,
        decimal? approvedAmount = null,
        string? note = null)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        var amountToApprove = approvedAmount
            ?? (role == ApprovalRole.Director && invoice.FirstApprovalAmount.HasValue
                ? invoice.FirstApprovalAmount.Value
                : invoice.Amount);
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var noteToApprove = normalizedNote
            ?? (role == ApprovalRole.Director && invoice.FirstApprovalBy is not null
                ? invoice.FirstApprovalNote
                : null);

        if (amountToApprove <= 0 || amountToApprove > invoice.Amount)
        {
            throw new InvalidOperationException("The approved amount must be greater than zero and no more than the invoiced amount.");
        }

        if (amountToApprove < invoice.Amount && noteToApprove is null)
        {
            throw new InvalidOperationException("A note is required when approving less than the invoiced amount.");
        }

        if (invoice.FirstApprovalBy is not null || amountToApprove > 500_000m)
        {
            return HandleHighValueApproval(invoice, approver, role, amountToApprove, noteToApprove);
        }

        var singleApprovalLimit = role switch
        {
            ApprovalRole.Manager => 50_000m,
            ApprovalRole.Director => 500_000m,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Only Managers and Directors can approve invoices.")
        };

        if (amountToApprove > singleApprovalLimit)
        {
            throw new InvalidOperationException(
                $"{role} cannot approve an amount above {singleApprovalLimit:N0} with a single approval.");
        }

        CompleteApproval(invoice, approver, amountToApprove, noteToApprove);
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
        Save(invoice);
        return invoice;
    }

    private Invoice HandleHighValueApproval(
        Invoice invoice,
        string approver,
        ApprovalRole role,
        decimal amountToApprove,
        string? note)
    {
        if (role == ApprovalRole.Manager)
        {
            if (!string.IsNullOrEmpty(invoice.FirstApprovalBy))
            {
                var errorMessage = string.Equals(invoice.FirstApprovalBy, approver, StringComparison.OrdinalIgnoreCase)
                    ? "A different Director is required for the second approval."
                    : "This invoice already has a first approval from a different Manager.";
                throw new InvalidOperationException(errorMessage);
            }

            invoice.FirstApprovalBy = approver;
            invoice.FirstApprovalAt = DateTime.UtcNow;
            invoice.FirstApprovalAmount = amountToApprove;
            invoice.FirstApprovalNote = note;
            invoice.Status = InvoiceStatus.PendingApproval;
            Save(invoice);
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

        var firstApprovalAmount = invoice.FirstApprovalAmount ?? invoice.Amount;
        if (firstApprovalAmount != amountToApprove)
        {
            throw new InvalidOperationException("The Director must confirm the same amount recorded by the Manager.");
        }

        if (note is not null && !string.Equals(invoice.FirstApprovalNote, note, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Director must confirm the same note recorded by the Manager.");
        }

        if (string.IsNullOrEmpty(invoice.SecondApprovalBy))
        {
            invoice.SecondApprovalBy = approver;
            invoice.SecondApprovalAt = DateTime.UtcNow;
            CompleteApproval(invoice, approver, amountToApprove, invoice.FirstApprovalNote);
            return invoice;
        }

        throw new InvalidOperationException("This high-value invoice is already approved.");
    }

    private void CompleteApproval(Invoice invoice, string approver, decimal amountToApprove, string? note)
    {
        invoice.ApprovedAmount = amountToApprove;
        invoice.ApprovalNote = note;
        invoice.Status = amountToApprove < invoice.Amount
            ? InvoiceStatus.PartiallyApproved
            : InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = null;
        Save(invoice);
    }

    private void Save(Invoice invoice)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        _repository.Update(invoice);
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
