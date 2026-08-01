using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/import")]
[ApiController]
public class ImportApiController : ControllerBase
{
    private readonly IBiometricImportService _svc;
    private readonly IWebHostEnvironment _env;

    public ImportApiController(IBiometricImportService svc, IWebHostEnvironment env)
    {
        _svc = svc;
        _env = env;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var path = await SaveTemp(file);
        try
        {
            var result = await _svc.PreviewFileAsync(path);
            return Ok(result);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [HttpPost("file")]
    public async Task<IActionResult> ImportFile(IFormFile file, [FromForm] DateTime fromDate, [FromForm] DateTime toDate)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var path = await SaveTemp(file);
        try
        {
            var result = await _svc.ImportFromFileAsync(path, fromDate, toDate);
            return Ok(result);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
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
