using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Exports, backup and restore.
///
/// Permissions reuse the existing catalogue on purpose. Adding a module to
/// <c>PermissionCatalogue</c> only seeds into a *new* database — the seed does not re-run on
/// one whose migration is already recorded — and that is exactly how the Import page once
/// 403'd for every user including Administrator. Exports sit under the module whose data they
/// contain; backup and restore under <c>Settings.Edit</c>, which only administrators hold.
/// </summary>
[Route("api/data")]
[SessionAuthorize]
public class DataApiController : ApiControllerBase
{
    private readonly IDataTransferService _data;
    private readonly IEmployeeImportService _import;

    public DataApiController(IDataTransferService data, IEmployeeImportService import)
    {
        _data = data;
        _import = import;
    }

    // ── Exports ──────────────────────────────────────────────────────────────

    /*
     * Exports are gated on the module's View, not its Export.
     *
     * Export permissions cannot be relied on here. Only Reports.Export and Overtime.Export
     * exist in a database seeded before the others were added — PermissionCatalogue seeds
     * once, and does not re-run — and Leave.Export was never in the catalogue at all. Gating
     * on them means an endpoint no administrator can reach, which is the same failure the
     * Import module hit.
     *
     * View is also the honest level: the existing Overtime Register and Summary screens
     * already build their CSV in the browser from data the page has loaded, so anyone who can
     * see a list can already export it. This makes the server agree with that rather than
     * pretending to a stricter rule it does not enforce.
     */

    [HttpGet("export/employees")]
    [SessionAuthorize(Modules.Employees, Actions.View)]
    public Task<IActionResult> ExportEmployees() => Download(ExportDataset.Employees, null, null);

    [HttpGet("export/attendance")]
    [SessionAuthorize(Modules.Attendance, Actions.View)]
    public Task<IActionResult> ExportAttendance([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Download(ExportDataset.Attendance, from, to);

    [HttpGet("export/leave")]
    [SessionAuthorize(Modules.Leave, Actions.View)]
    public Task<IActionResult> ExportLeave([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Download(ExportDataset.Leave, from, to);

    [HttpGet("export/overtime")]
    [SessionAuthorize(Modules.Overtime, Actions.View)]
    public Task<IActionResult> ExportOvertime([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Download(ExportDataset.Overtime, from, to);

    private async Task<IActionResult> Download(ExportDataset set, DateTime? from, DateTime? to)
    {
        var r = await _data.ExportAsync(set, from, to);
        return r.IsSuccess && r.Data != null
            ? File(r.Data.Content, r.Data.ContentType, r.Data.FileName)
            : BadRequest(r.ErrorMessage);
    }

    // ── Backup / restore ─────────────────────────────────────────────────────

    [HttpGet("backup")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> Backup()
    {
        var r = await _data.CreateBackupAsync();
        return r.IsSuccess && r.Data != null
            ? File(r.Data.Content, r.Data.ContentType, r.Data.FileName)
            : BadRequest(r.ErrorMessage);
    }

    [HttpPost("restore/preview")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> RestorePreview()
    {
        var bytes = await ReadUpload();
        if (bytes == null) return BadRequest("No backup archive was uploaded.");

        var r = await _data.PreviewRestoreAsync(bytes);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("restore")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Restore()
    {
        var bytes = await ReadUpload();
        if (bytes == null) return BadRequest("No backup archive was uploaded.");

        // Typing the word is the last gate before data is replaced. It is deliberately not a
        // checkbox: a checkbox is one careless click, and this cannot be undone.
        var confirm = Request.Form["confirm"].ToString();
        if (!string.Equals(confirm, "RESTORE", StringComparison.Ordinal))
            return BadRequest("Type RESTORE to confirm. Nothing was changed.");

        var tables = Request.Form["tables"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var r = await _data.RestoreAsync(bytes, tables);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    private async Task<byte[]?> ReadUpload()
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0) return null;

        var file = Request.Form.Files["file"] ?? Request.Form.Files[0];
        if (file == null || file.Length == 0) return null;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    // ── Employee import ──────────────────────────────────────────────────────

    [HttpGet("employees/template")]
    [SessionAuthorize(Modules.Employees, Actions.Create)]
    public IActionResult Template()
    {
        var r = _import.GetTemplate();
        return r.IsSuccess && r.Data != null
            ? File(r.Data.Content, r.Data.ContentType, r.Data.FileName)
            : BadRequest(r.ErrorMessage);
    }

    [HttpPost("employees/preview")]
    [SessionAuthorize(Modules.Employees, Actions.Create)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> PreviewEmployees()
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest("No file uploaded. Choose a CSV or Excel file.");

        var file = Request.Form.Files["file"] ?? Request.Form.Files[0];
        if (file == null || file.Length == 0) return BadRequest("The uploaded file is empty.");

        await using var stream = file.OpenReadStream();
        var r = await _import.PreviewAsync(stream, file.FileName);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost("employees/import")]
    [SessionAuthorize(Modules.Employees, Actions.Create)]
    public async Task<IActionResult> ImportEmployees([FromBody] List<EmployeeImportRowDto> rows)
    {
        var r = await _import.ImportAsync(rows);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
