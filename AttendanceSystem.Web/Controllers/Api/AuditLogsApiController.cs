using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Audit trail. Read-only by design — an audit log that can be edited is not an audit log,
/// so there is deliberately no create, update or delete endpoint here.
/// </summary>
[Route("api/audit-logs")]
[SessionAuthorize]
public class AuditLogsApiController : ApiControllerBase
{
    private readonly IAuditService _audit;

    public AuditLogsApiController(IAuditService audit) => _audit = audit;

    /// <summary>
    /// One page of the trail, filtered and searched in the database.
    ///
    /// This endpoint used to return up to 1,000 rows for the browser to filter. The audit table
    /// is the only one in the system that grows without bound, so it is the last place that
    /// should be fetched whole — a year of activity would be megabytes on every page load.
    /// </summary>
    [HttpGet]
    [SessionAuthorize(Modules.AuditLogs, Actions.View)]
    public async Task<IActionResult> Get(
        [FromQuery] string? module,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var r = await _audit.GetPagedAsync(module, search,
            new PageRequest { Page = page, PageSize = pageSize });

        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
