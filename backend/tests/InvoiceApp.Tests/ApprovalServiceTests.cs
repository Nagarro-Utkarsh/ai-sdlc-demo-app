using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_ManagerAtLimit_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50_000m));

        var result = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_ManagerAboveLimit_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50_000.01m);
        invoice.DecisionNote = "existing note";
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", ApprovalRole.Manager));

        Assert.Contains("Manager", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Approve_DirectorAtLimit_ApprovesInvoice()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_000m));

        var result = service.Approve("INV-1", "demo.director", ApprovalRole.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_DirectorAboveLimit_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_000.01m);
        invoice.DecidedBy = "existing user";
        invoice.DecisionNote = "existing note";
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director", ApprovalRole.Director));

        Assert.Contains("Director", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Approve_HighValueInvoice_ManagerFirstLeavesInvoicePendingAndRecordsFirstApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal("demo.manager", result.FirstApprovalBy);
        Assert.NotNull(result.FirstApprovalAt);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
    }

    [Fact]
    public void Approve_HighValueInvoice_DirectorSecondApprovesInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.director", ApprovalRole.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.SecondApprovalBy);
        Assert.NotNull(result.SecondApprovalAt);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_HighValueInvoice_DirectorFirstAttempt_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.DecidedBy = "existing user";
        invoice.DecisionNote = "existing note";
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director", ApprovalRole.Director));

        Assert.Contains("prior Manager approval", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Approve_HighValueInvoice_SameUserSecondAttempt_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        invoice.DecidedBy = "existing user";
        invoice.DecisionNote = "existing note";
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "DEMO.MANAGER", ApprovalRole.Director));

        Assert.Contains("different Director", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Approve_HighValueInvoice_DifferentManagerSecondAttempt_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "another.manager", ApprovalRole.Manager));

        Assert.Contains("different Manager", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Reject_AfterHighValueFirstApproval_PreservesFirstApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        var service = ServiceFor(invoice);

        var result = service.Reject("INV-1", "demo.admin", "Supporting documents are missing");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.manager", result.FirstApprovalBy);
        Assert.NotNull(result.FirstApprovalAt);
        Assert.Equal("demo.admin", result.DecidedBy);
        Assert.Equal("Supporting documents are missing", result.DecisionNote);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.manager", ApprovalRole.Manager));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.manager", ApprovalRole.Manager));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.manager", "  "));
    }

    private static object Snapshot(Invoice invoice) => new
    {
        invoice.Id,
        invoice.CustomerName,
        invoice.Amount,
        invoice.Status,
        invoice.CreatedAt,
        invoice.FirstApprovalBy,
        invoice.FirstApprovalAt,
        invoice.SecondApprovalBy,
        invoice.SecondApprovalAt,
        invoice.DecidedBy,
        invoice.DecidedAt,
        invoice.DecisionNote
    };
}
