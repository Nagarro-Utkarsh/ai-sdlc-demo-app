using InvoiceApp.Api.Auth;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InvoiceApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ApiControllerBase
{
    private readonly ApprovalService _approvals;

    public ApprovalsController(ApprovalService approvals)
    {
        _approvals = approvals;
    }

    public record RejectRequest(string Reason);
    public record ApproveRequest(decimal? ApprovedAmount, string? Note);

    [HttpPost("{invoiceId}/approve")]
    [RequireRole("Manager", "Director")]
    public ActionResult<Invoice> Approve(
        string invoiceId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApproveRequest? request)
    {
        var session = HttpContext.GetSession();
        var role = Enum.Parse<ApprovalRole>(session.Role);
        return Handle(() => _approvals.Approve(
            invoiceId,
            session.Username,
            role,
            request?.ApprovedAmount,
            request?.Note));
    }

    [HttpPost("{invoiceId}/reject")]
    [RequireRole("Manager", "Director", "Admin")]
    public ActionResult<Invoice> Reject(string invoiceId, [FromBody] RejectRequest request) =>
        Handle(() => _approvals.Reject(invoiceId, HttpContext.GetSession().Username, request.Reason));
}
