using AttendanceSystem.Application.Interfaces;
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

    [HttpGet]
    [SessionAuthorize(Modules.AuditLogs, Actions.View)]
    public async Task<IActionResult> Get([FromQuery] string? module, [FromQuery] int count = 200)
    {
        // The row limit applies either way — it was previously ignored once a module was
        // chosen, so picking a module quietly returned the entire history for it.
        var take = Math.Clamp(count, 1, 1000);

        var r = string.IsNullOrWhiteSpace(module)
            ? await _audit.GetRecentAsync(take)
            : await _audit.GetByModuleAsync(module, take);

        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
