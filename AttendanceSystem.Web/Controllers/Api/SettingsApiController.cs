using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

[Route("api/settings")]
[SessionAuthorize]
public class SettingsApiController : ApiControllerBase
{
    private readonly ISettingsService _svc;
    private readonly IEmailService _email;
    private readonly ISmsService _sms;

    public SettingsApiController(ISettingsService svc, IEmailService email, ISmsService sms)
    {
        _svc = svc;
        _email = email;
        _sms = sms;
    }

    [HttpGet]
    [SessionAuthorize(Modules.Settings, Actions.View)]
    public async Task<IActionResult> Get()
    {
        var r = await _svc.GetAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> Save([FromBody] CompanySettingsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await _svc.SaveAsync(dto, CurrentUserId);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>
    /// Sends a trial message so mail can be proven to work before anyone needs it.
    ///
    /// Gated on Edit rather than View: it sends real mail through the configured server, and
    /// an endpoint that sends to any address given to it is not a read.
    /// </summary>
    [HttpPost("test-email")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> TestEmail([FromBody] SendTestEmailDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Save first, then test — otherwise the test proves the *old* settings work while
        // the administrator reads it as confirming what they just typed.
        var r = await _email.SendTestEmailAsync(dto.ToEmail.Trim());
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    /// <summary>Sends a trial SMS. Edit-gated for the same reason as the mail test.</summary>
    [HttpPost("test-sms")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> TestSms([FromBody] SendTestSmsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var r = await _sms.SendTestSmsAsync(dto.ToNumber.Trim());
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("upload-logo")]
    [SessionAuthorize(Modules.Settings, Actions.Edit)]
    public async Task<IActionResult> UploadLogo()
    {
        var file = Request.Form.Files["file"] ?? Request.Form.Files["File"] ?? Request.Form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
            return BadRequest("No image file selected.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg" };
        if (!allowed.Contains(ext))
            return BadRequest("Invalid image format. Supported formats: PNG, JPG, JPEG, GIF, WEBP, SVG.");

        var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var assetsImages = Path.Combine(wwwroot, "assets", "images");
        var imagesDir = Path.Combine(wwwroot, "images");
        Directory.CreateDirectory(assetsImages);
        Directory.CreateDirectory(imagesDir);

        var targetPath1 = Path.Combine(assetsImages, "samanmal_logo.png");
        var targetPath2 = Path.Combine(imagesDir, "samanmal_logo.png");
        var targetPath3 = Path.Combine(assetsImages, "logo.png");

        using (var stream = new FileStream(targetPath1, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        System.IO.File.Copy(targetPath1, targetPath2, overwrite: true);
        System.IO.File.Copy(targetPath1, targetPath3, overwrite: true);

        return Ok(new { message = "Logo updated successfully!", logoUrl = "/assets/images/samanmal_logo.png?v=" + DateTime.Now.Ticks });
    }
}
