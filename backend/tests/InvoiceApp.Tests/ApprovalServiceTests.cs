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
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        var error = Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));

        Assert.Contains("only drafts can be submitted", error.Message);
    }

    [Fact]
    public void Approve_ManagerAtLimit_SetsStatusApproverAndTimestamp()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50_000m));
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
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
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_000m));
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.director", ApprovalRole.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ManagerPartialAtLimit_StoresOriginalApprovedAmountsAndNote()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 75_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager, 50_000m, "Disputed line item");

        Assert.Equal(InvoiceStatus.PartiallyApproved, result.Status);
        Assert.Equal(75_000m, result.Amount);
        Assert.Equal(50_000m, result.ApprovedAmount);
        Assert.Equal("Disputed line item", result.ApprovalNote);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_DirectorPartialAtLimitOnLargeInvoice_ApprovesInOneStep()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 900_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.director", ApprovalRole.Director, 500_000m, "Disputed balance");

        Assert.Equal(InvoiceStatus.PartiallyApproved, result.Status);
        Assert.Equal(900_000m, result.Amount);
        Assert.Equal(500_000m, result.ApprovedAmount);
        Assert.Equal("Disputed balance", result.ApprovalNote);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Approve_PartialAmountWithoutNote_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 75_000m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", ApprovalRole.Manager, 50_000m));

        Assert.Contains("A note is required", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    public static IEnumerable<object[]> InvalidApprovedAmounts => new object[][]
    {
        new object[] { 0m },
        new object[] { -1m },
        new object[] { 75_000.01m }
    };

    [Theory]
    [MemberData(nameof(InvalidApprovedAmounts))]
    public void Approve_InvalidApprovedAmount_RejectsWithoutChangingInvoice(decimal approvedAmount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 75_000m);
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director", ApprovalRole.Director, approvedAmount, "Note"));

        Assert.Contains("approved amount", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ManagerHighValuePartialThenDirectorConfirmation_CompletesPartialApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 900_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var firstApproval = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager, 600_000m, "Disputed line item");

        Assert.Equal(InvoiceStatus.PendingApproval, firstApproval.Status);
        Assert.Equal(900_000m, firstApproval.Amount);
        Assert.Equal(600_000m, firstApproval.FirstApprovalAmount);
        Assert.Equal("Disputed line item", firstApproval.FirstApprovalNote);
        Assert.Null(firstApproval.ApprovedAmount);
        Assert.Null(firstApproval.DecidedBy);

        var finalApproval = service.Approve("INV-1", "demo.director", ApprovalRole.Director, 600_000m);

        Assert.Equal(InvoiceStatus.PartiallyApproved, finalApproval.Status);
        Assert.Equal(600_000m, finalApproval.ApprovedAmount);
        Assert.Equal("Disputed line item", finalApproval.ApprovalNote);
        Assert.Equal("demo.manager", finalApproval.FirstApprovalBy);
        Assert.Equal("demo.director", finalApproval.SecondApprovalBy);
        Assert.Equal("demo.director", finalApproval.DecidedBy);
        Assert.Equal(2, repository.UpdateCount);
    }

    [Fact]
    public void Approve_DirectorChangesHighValueApprovedAmount_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 900_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.manager", ApprovalRole.Manager, 600_000m, "Disputed line item");
        var before = Snapshot(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director", ApprovalRole.Director, 500_000m, "Disputed line item"));

        Assert.Contains("same amount", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(1, repository.UpdateCount);
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
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.manager", ApprovalRole.Manager);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal("demo.manager", result.FirstApprovalBy);
        Assert.NotNull(result.FirstApprovalAt);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Approve_HighValueInvoice_DirectorSecondApprovesInvoice(string? secondApprovalBy)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        invoice.SecondApprovalBy = secondApprovalBy;
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", "demo.director", ApprovalRole.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.SecondApprovalBy);
        Assert.NotNull(result.SecondApprovalAt);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(1, repository.UpdateCount);
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
    public void Approve_HighValueInvoice_FirstManagerCannotRepeatApproval_RejectsWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600_000m);
        invoice.FirstApprovalBy = "demo.manager";
        invoice.FirstApprovalAt = DateTime.UtcNow.AddMinutes(-1);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "DEMO.MANAGER", ApprovalRole.Manager));

        Assert.Contains("different Director", error.Message);
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

        var error = Assert.Throws<KeyNotFoundException>(
            () => service.Approve("INV-404", "demo.manager", ApprovalRole.Manager));

        Assert.Contains("Invoice INV-404 was not found", error.Message);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));
        var service = new ApprovalService(repository);

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var error = Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.manager", "  "));

        Assert.Contains("A reason is required", error.Message);
    }

    [Fact]
    public void Reject_RejectsInvoiceThatIsNotPendingAndPreservesState()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Reject("INV-1", "demo.manager", "Duplicate invoice"));

        Assert.Contains("only pending invoices can be decided", error.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    private static object Snapshot(Invoice invoice) => new
    {
        invoice.Id,
        invoice.CustomerName,
        invoice.Amount,
        invoice.ApprovedAmount,
        invoice.ApprovalNote,
        invoice.Status,
        invoice.CreatedAt,
        invoice.FirstApprovalBy,
        invoice.FirstApprovalAt,
        invoice.SecondApprovalBy,
        invoice.SecondApprovalAt,
        invoice.FirstApprovalAmount,
        invoice.FirstApprovalNote,
        invoice.DecidedBy,
        invoice.DecidedAt,
        invoice.DecisionNote
    };
}
