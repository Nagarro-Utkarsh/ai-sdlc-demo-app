namespace InvoiceApp.Domain;

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? ApprovalNote { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? FirstApprovalBy { get; set; }
    public DateTime? FirstApprovalAt { get; set; }
    public string? SecondApprovalBy { get; set; }
    public DateTime? SecondApprovalAt { get; set; }
    public decimal? FirstApprovalAmount { get; set; }
    public string? FirstApprovalNote { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}
