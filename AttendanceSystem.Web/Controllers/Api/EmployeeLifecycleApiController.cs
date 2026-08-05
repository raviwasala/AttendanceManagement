using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Employee profile, transfers, status, resignation and documents.
///
/// Permissions reuse the Employees module rather than adding one: a module added to
/// PermissionCatalogue only seeds into a *new* database, so on an existing one the rows would
/// not exist and every endpoint here would 403 for everybody, including Administrator.
/// </summary>
[Route("api/employees")]
[SessionAuthorize]
public class EmployeeLifecycleApiController : ApiControllerBase
{
    private readonly IEmployeeLifecycleService _svc;
    public EmployeeLifecycleApiController(IEmployeeLifecycleService svc) => _svc = svc;

    [HttpGet("{id:int}/profile")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Profile(int id)
    {
        var r = await _svc.GetProfileAsync(id);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id:int}/history")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> History(int id)
    {
        var r = await _svc.GetHistoryAsync(id);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("transfer")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> Transfer([FromBody] TransferEmployeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.TransferAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("status")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> ChangeStatus([FromBody] ChangeEmployeeStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ChangeStatusAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("resign")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> Resign([FromBody] ResignEmployeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.ResignAsync(dto);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id:int}/rejoin")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    public async Task<IActionResult> Rejoin(int id, [FromBody] RejoinRequest body)
    {
        var r = await _svc.RejoinAsync(id, body.EffectiveDate, body.Reason ?? string.Empty);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    public record RejoinRequest(DateTime EffectiveDate, string? Reason);

    // ── Documents ─────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/documents")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> Documents(int id)
    {
        var r = await _svc.GetDocumentsAsync(id);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id:int}/documents")]
    [SessionAuthorize(Modules.Employees, Actions.Edit)]
    [RequestSizeLimit(12 * 1024 * 1024)]   // a little over the service's 10 MB, so its message wins
    public async Task<IActionResult> UploadDocument(int id)
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest("No file uploaded.");

        var file = Request.Form.Files["file"] ?? Request.Form.Files[0];
        if (file == null || file.Length == 0) return BadRequest("The uploaded file is empty.");

        Enum.TryParse<EmployeeDocumentType>(Request.Form["documentType"], out var type);
        if (type == 0) type = EmployeeDocumentType.Other;

        DateTime? expiry = DateTime.TryParse(Request.Form["expiryDate"], out var d) ? d : null;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var r = await _svc.UploadDocumentAsync(
            id, type,
            Request.Form["title"].ToString(),
            file.FileName, file.ContentType, ms.ToArray(),
            expiry,
            Request.Form["notes"].ToString());

        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("documents/{documentId:int}/download")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public async Task<IActionResult> DownloadDocument(int documentId)
    {
        var r = await _svc.DownloadDocumentAsync(documentId);
        if (!r.IsSuccess || r.Data == null) return BadRequest(r.ErrorMessage);

        // Attachment, never inline: a stored file rendered in the browser under this origin
        // would run with the application's session if it turned out to be markup.
        return File(r.Data.Content, "application/octet-stream", r.Data.FileName);
    }

    [HttpDelete("documents/{documentId:int}")]
    [SessionAuthorize(Modules.Employees, Actions.Delete)]
    public async Task<IActionResult> DeleteDocument(int documentId)
    {
        var r = await _svc.DeleteDocumentAsync(documentId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }
}
