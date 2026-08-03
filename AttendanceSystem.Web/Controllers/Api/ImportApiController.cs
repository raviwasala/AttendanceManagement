using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/import")]
[SessionAuthorize]
public class ImportApiController : ApiControllerBase
{
    private readonly IBiometricImportService _svc;
    private readonly IWebHostEnvironment _env;

    public ImportApiController(IBiometricImportService svc, IWebHostEnvironment env)
    {
        _svc = svc;
        _env = env;
    }

    [HttpPost("preview")]
    [SessionAuthorize(Modules.Import, Actions.View)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Preview()
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
        {
            return BadRequest("No file uploaded. Please choose a biometric file.");
        }

        var file = Request.Form.Files["file"] ?? Request.Form.Files["File"] ?? Request.Form.Files[0];
        if (file == null || file.Length == 0)
        {
            return BadRequest("Uploaded file is empty.");
        }

        DateTime.TryParse(Request.Form["fromDate"], out var fromDate);
        DateTime.TryParse(Request.Form["toDate"], out var toDate);

        var path = await SaveTemp(file);
        try
        {
            var start = fromDate == default ? new DateTime(2000, 1, 1) : fromDate;
            var end = toDate == default ? DateTime.Today : toDate;
            var result = await _svc.PreviewFileAsync(path, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [HttpPost("file")]
    [SessionAuthorize(Modules.Import, Actions.Create)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ImportFile()
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
        {
            return BadRequest("No file uploaded. Please choose a biometric file.");
        }

        var file = Request.Form.Files["file"] ?? Request.Form.Files["File"] ?? Request.Form.Files[0];
        if (file == null || file.Length == 0)
        {
            return BadRequest("Uploaded file is empty.");
        }

        DateTime.TryParse(Request.Form["fromDate"], out var fromDate);
        DateTime.TryParse(Request.Form["toDate"], out var toDate);

        var path = await SaveTemp(file);
        try
        {
            var start = fromDate == default ? new DateTime(2000, 1, 1) : fromDate;
            var end = toDate == default ? DateTime.Today : toDate;
            var result = await _svc.ImportFromFileAsync(path, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [HttpPost("process-edited")]
    [SessionAuthorize(Modules.Import, Actions.Create)]
    public async Task<IActionResult> ProcessEdited([FromBody] List<BiometricPunchDto> punches)
    {
        if (punches == null || punches.Count == 0) return BadRequest("No punch records provided for processing.");

        try
        {
            var result = await _svc.ProcessEditedPunchesAsync(punches);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<string> SaveTemp(IFormFile file)
    {
        var uploads = Path.Combine(_env.ContentRootPath, "TempUploads");
        Directory.CreateDirectory(uploads);
        var path = Path.Combine(uploads, $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
        using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream);
        return path;
    }
}
